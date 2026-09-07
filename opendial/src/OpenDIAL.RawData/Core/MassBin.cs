using System.Collections.Generic;

namespace CompMs.RawDataHandler.Core;

/// <summary>
/// Accumulator for one m/z bin: keeps the m/z of the most intense contribution and the
/// summed intensity. Same contract as the closed RawDataHandler type.
/// </summary>
public sealed class MassBin
{
    private double _mz;
    private double _baseIntensity;
    private double _summedIntensity;

    public double Mz => _mz;
    public double BaseIntensity => _baseIntensity;
    public double SummedIntensity => _summedIntensity;

    public MassBin(double mz, double intensity) {
        _mz = mz;
        _baseIntensity = _summedIntensity = intensity;
    }

    public void Add(double mz, double intensity) {
        _summedIntensity += intensity;
        if (intensity > _baseIntensity) {
            _mz = mz;
            _baseIntensity = intensity;
        }
    }

    public void Initialize(double mz, double intensity) {
        _mz = mz;
        _baseIntensity = _summedIntensity = intensity;
    }
}

/// <summary>Object pool for <see cref="MassBin"/> to limit allocations during accumulation.</summary>
public sealed class MassBinPool
{
    private readonly Stack<MassBin> _pool = new Stack<MassBin>();

    public MassBin Get(double mz, double intensity) {
        if (_pool.Count > 0) {
            var bin = _pool.Pop();
            bin.Initialize(mz, intensity);
            return bin;
        }
        return new MassBin(mz, intensity);
    }

    public void Return(MassBin bin) => _pool.Push(bin);

    public void BatchReturn(IEnumerable<MassBin> bins) {
        foreach (var bin in bins) {
            _pool.Push(bin);
        }
    }
}
