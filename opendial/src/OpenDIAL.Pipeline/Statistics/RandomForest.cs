namespace OpenDIAL.Pipeline.Statistics;

public sealed record ForestImportance(int FeatureId, string Label, string Group, double MeanDecreaseAccuracy, double MeanDecreaseGini);

public sealed record RandomForestResult(
    IReadOnlyList<string> Classes,
    IReadOnlyList<ForestImportance> Importance,
    double OutOfBagError,
    IReadOnlyList<(string Class, double Error)> ClassErrors,
    int[,] Confusion,
    IReadOnlyList<string> OutOfBagPredictions,
    int Trees,
    int FeaturesPerSplit,
    string Message);

/// <summary>
/// A random forest classifier over the injections: bootstrap samples, a random draw of features at
/// every split, the Gini impurity to choose it, and the out-of-bag injections to say how well it
/// classifies without a held-out set. Importance is the fall in out-of-bag accuracy when a
/// feature's values are shuffled, the measure MetaboAnalyst reports, beside the Gini decrease.
///
/// With a handful of injections and thousands of features the forest will fit the batch; the
/// out-of-bag error is the number that says whether it would fit the next one.
/// </summary>
public static class RandomForest
{
    private sealed class Node
    {
        public int Feature = -1;
        public double Threshold;
        public Node? Left;
        public Node? Right;
        public int Class;
        public bool IsLeaf => Left is null;
    }

    public static RandomForestResult Compute(DataMatrix matrix, int trees = 500, int? featuresPerSplit = null, int seed = 20260907)
    {
        ArgumentNullException.ThrowIfNull(matrix);
        var n = matrix.SampleCount;
        var p = matrix.FeatureCount;
        var classes = matrix.Samples.Select(AnalysisTable.ClassOf).ToArray();
        var distinct = classes.Distinct().OrderBy(c => c, StringComparer.OrdinalIgnoreCase).ToList();
        if (distinct.Count < 2 || n < 4)
        {
            return new RandomForestResult(distinct, Array.Empty<ForestImportance>(), double.NaN, Array.Empty<(string, double)>(), new int[0, 0], Array.Empty<string>(), 0, 0,
                distinct.Count < 2 ? "Needs at least two classes." : $"Needs at least four injections; this batch has {n}.");
        }
        var y = classes.Select(c => distinct.IndexOf(c)).ToArray();
        var mtry = Math.Clamp(featuresPerSplit ?? (int)Math.Max(1, Math.Round(Math.Sqrt(p))), 1, p);
        var rng = new Random(seed);
        var x = matrix.Values;

        var oobVotes = new int[n, distinct.Count];
        var gini = new double[p];
        var accuracyDrop = new double[p];
        var usedCount = new int[p];

        for (var t = 0; t < trees; t++)
        {
            var bag = new int[n];
            var inBag = new bool[n];
            for (var i = 0; i < n; i++) { bag[i] = rng.Next(n); inBag[bag[i]] = true; }
            var oob = Enumerable.Range(0, n).Where(i => !inBag[i]).ToList();
            var used = new HashSet<int>();
            var root = Grow(x, y, bag, distinct.Count, mtry, rng, gini, used, 0);
            if (oob.Count == 0) continue;
            var correct = 0;
            foreach (var i in oob)
            {
                var predicted = Predict(root, x, i);
                oobVotes[i, predicted]++;
                if (predicted == y[i]) correct++;
            }
            // permutation importance: shuffle one feature over the out-of-bag rows and re-predict
            foreach (var j in used)
            {
                var shuffled = oob.OrderBy(_ => rng.Next()).ToList();
                var permutedCorrect = 0;
                for (var k = 0; k < oob.Count; k++)
                {
                    var i = oob[k];
                    var replacement = x[shuffled[k], j];
                    var predicted = PredictWithOverride(root, x, i, j, replacement);
                    if (predicted == y[i]) permutedCorrect++;
                }
                accuracyDrop[j] += (double)(correct - permutedCorrect) / oob.Count;
                usedCount[j]++;
            }
        }

        var predictions = new string[n];
        var errors = 0;
        var voted = 0;
        var confusion = new int[distinct.Count, distinct.Count];
        var classErrors = new (string, double)[distinct.Count];
        var classWrong = new int[distinct.Count];
        var classTotal = new int[distinct.Count];
        for (var i = 0; i < n; i++)
        {
            var best = -1;
            var bestVotes = 0;
            for (var c = 0; c < distinct.Count; c++) if (oobVotes[i, c] > bestVotes) { bestVotes = oobVotes[i, c]; best = c; }
            if (best < 0) { predictions[i] = "(never out of bag)"; continue; }
            predictions[i] = distinct[best];
            voted++;
            confusion[y[i], best]++;
            classTotal[y[i]]++;
            if (best != y[i]) { errors++; classWrong[y[i]]++; }
        }
        for (var c = 0; c < distinct.Count; c++) classErrors[c] = (distinct[c], classTotal[c] == 0 ? double.NaN : (double)classWrong[c] / classTotal[c]);
        var oobError = voted == 0 ? double.NaN : (double)errors / voted;

        var importance = new List<ForestImportance>(p);
        for (var j = 0; j < p; j++)
        {
            var feature = matrix.Features[j];
            importance.Add(new ForestImportance(feature.Id, AnalysisTable.LabelOf(feature),
                string.IsNullOrWhiteSpace(feature.Ontology) ? "(no class)" : feature.Ontology,
                usedCount[j] == 0 ? 0 : accuracyDrop[j] / trees, gini[j] / trees));
        }
        var message = $"{trees} trees, {mtry} of {p} features tried at each split · out-of-bag error {oobError:P1} over {voted} injections"
            + (oobError > 0.3 ? " — the forest does not tell the classes apart much better than chance would." : string.Empty);
        return new RandomForestResult(distinct, importance.OrderByDescending(i => i.MeanDecreaseAccuracy).ThenByDescending(i => i.MeanDecreaseGini).ToList(),
            oobError, classErrors, confusion, predictions, trees, mtry, message);
    }

    private static Node Grow(double[,] x, int[] y, int[] rows, int classes, int mtry, Random rng, double[] gini, HashSet<int> used, int depth)
    {
        var counts = new int[classes];
        foreach (var i in rows) counts[y[i]]++;
        var majority = Array.IndexOf(counts, counts.Max());
        var impurity = Gini(counts, rows.Length);
        if (impurity <= 0 || rows.Length < 2 || depth > 25) return new Node { Class = majority };

        var p = x.GetLength(1);
        var candidates = new HashSet<int>();
        while (candidates.Count < mtry) candidates.Add(rng.Next(p));
        var bestFeature = -1;
        var bestThreshold = 0.0;
        var bestGain = 1e-12;
        int[]? bestLeft = null;
        int[]? bestRight = null;
        foreach (var j in candidates)
        {
            var sorted = rows.OrderBy(i => x[i, j]).ToArray();
            var leftCounts = new int[classes];
            var rightCounts = (int[])counts.Clone();
            for (var k = 0; k < sorted.Length - 1; k++)
            {
                leftCounts[y[sorted[k]]]++;
                rightCounts[y[sorted[k]]]--;
                if (x[sorted[k], j] == x[sorted[k + 1], j]) continue;
                var nl = k + 1;
                var nr = sorted.Length - nl;
                var gain = impurity - (nl * Gini(leftCounts, nl) + nr * Gini(rightCounts, nr)) / sorted.Length;
                if (gain > bestGain)
                {
                    bestGain = gain;
                    bestFeature = j;
                    bestThreshold = (x[sorted[k], j] + x[sorted[k + 1], j]) / 2;
                    bestLeft = sorted.Take(nl).ToArray();
                    bestRight = sorted.Skip(nl).ToArray();
                }
            }
        }
        if (bestFeature < 0 || bestLeft is null || bestRight is null) return new Node { Class = majority };
        gini[bestFeature] += bestGain * rows.Length;
        used.Add(bestFeature);
        return new Node
        {
            Feature = bestFeature,
            Threshold = bestThreshold,
            Class = majority,
            Left = Grow(x, y, bestLeft, classes, mtry, rng, gini, used, depth + 1),
            Right = Grow(x, y, bestRight, classes, mtry, rng, gini, used, depth + 1),
        };
    }

    private static double Gini(int[] counts, int total)
    {
        if (total == 0) return 0;
        double g = 1;
        foreach (var c in counts) { var f = (double)c / total; g -= f * f; }
        return g;
    }

    private static int Predict(Node node, double[,] x, int row)
    {
        while (!node.IsLeaf) node = x[row, node.Feature] <= node.Threshold ? node.Left! : node.Right!;
        return node.Class;
    }

    private static int PredictWithOverride(Node node, double[,] x, int row, int feature, double value)
    {
        while (!node.IsLeaf)
        {
            var v = node.Feature == feature ? value : x[row, node.Feature];
            node = v <= node.Threshold ? node.Left! : node.Right!;
        }
        return node.Class;
    }
}
