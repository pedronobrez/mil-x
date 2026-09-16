---
title: Glossário
section: Reference
order: 45
summary: Definições curtas dos termos usados na aplicação e neste manual.
---

# Glossário

**Aduto** — a forma iônica em que um composto foi detectado: `[M+H]+`, `[M+Na]+`, `[M+NH4]+`, …

**Alinhamento** — casar os picos de toda injeção numa lista de features por tempo de retenção e m/z. Veja [[algorithms#Alinhamento e preenchimento de lacunas]].

**Alignment spot** — a palavra do MS-DIAL para uma feature.

**Anotação** — o nome dado a uma feature ao casar o seu espectro contra uma biblioteca; também o ato. Veja [[annotation]].

**Área, razão de** — a área de pico de um analito dividida pela área do padrão interno da sua classe na mesma injeção; a *abundância relativa* em que um resultado de lipidômica é reportado. Veja [[internal-standards]].

**ASCA** — ANOVA-simultaneous component analysis: a matriz desmontada no que cada fator de um desenho explica, cada parte vista com os seus próprios componentes. Veja [[two-factor-analysis]].

**Auto-scaling** — dividir toda feature pelo seu desvio padrão depois de centrar, de modo que cada uma pesa o mesmo num modelo; escalonamento de *variância unitária*.

**BioPAN** — a análise de vias do LIPID MAPS para lipidômica: reações da rede de lipídios ponderadas por produto sobre reagente e comparadas entre duas condições. Embutida na página [[pathways]].

**Candidato** — um casamento de biblioteca guardado para uma feature; o melhor é a anotação.

**Centroide** — um espectro reduzido a um m/z e uma intensidade por pico, por oposição a perfil.

**Classe** — o grupo em texto livre de uma injeção, que a estatística compara.

**Curadoria** — tudo o que um revisor muda: marcações, comentários, nomes, janelas de integração, separações.

**Deconvolução** — MS2Dec: atribuir a cada pico um espectro de íons-produto limpo das contribuições coeluídas.

**Deriva** — a mudança lenta da resposta do instrumento ao longo de uma sequência; corrigida contra as injeções de QC. Veja [[statistics-workspace#Drift correction]].

**Escalonamento de Pareto** — dividir toda feature pela raiz quadrada do seu desvio padrão depois de centrar; entre o auto-scaling e só centrar.

**Escores** — onde cada injeção cai nos componentes de um modelo.

**FDR** — a taxa de falsas descobertas: a fração das features chamadas significativas que se espera serem falsas. Benjamini–Hochberg controla-a, e o seu p ajustado é o padrão nas páginas de teste.

**Feature** — um íon de composto ao longo das injeções: um tempo de retenção, um m/z, um nome, um pico por injeção.

**Fold change** — a razão da média de uma feature numa classe pela sua média noutra, nos valores normalizados antes da transformação; o seu log2 no volcano plot.

**IDA** — o nome da SCIEX para DDA.

**Imputação** — substituir um valor ausente por uma estimativa: uma fração do mínimo da feature, a sua média, ou a média das features mais próximas. Veja [[one-factor-analysis#Processamento dos dados]].

**Interação** — quando o efeito de um fator depende do nível do outro; o tratamento que funciona num tempo e não noutro.

**Íon molecular** — uma feature que não é isótopo de outra; o filtro **Molecular ion** guarda só estas.

**KNN** — k vizinhos mais próximos; como imputação, a média das *k* features que melhor se correlacionam com a que tem o valor ausente.

**Loadings** — quanto cada feature contribui para um componente, nos gráficos de componentes principais e discriminantes.

**Lote** — a lista de injeções que um projeto processa junto; também o número que agrupa as injeções corridas numa sequência, para a correção de deriva.

**Método** — os parâmetros de processamento, como um arquivo de texto `Key: value`. Veja [[method-parameters]].

**Método de Stouffer** — combinar Z-scores somando-os e dividindo pela raiz quadrada do seu número; como o Z de uma via é feito a partir dos das suas reações.

**MS2Dec** — o algoritmo de deconvolução do MS-DIAL.

**MSP** — o formato de biblioteca espectral: um registro por composto com o seu precursor, nome, fórmula, tempo de retenção e espectro de produto.

**Nível de anotação** — confident, suggested ou m/z only, lido do nome que o MS-DIAL escreveu. Veja [[concepts#Níveis de anotação]].

**Ontologia** — a palavra do MS-DIAL para a classe do composto; para um lipídio, a classe de lipídio.

**Ordem analítica** — a posição de uma injeção na sequência em que foi corrida.

**Padrão interno** — um composto de uma classe de lipídio, marcado ou de cadeia ímpar, adicionado a toda amostra na mesma quantidade para que os analitos da classe possam ser reportados como razões a ele. Veja [[internal-standards]].

**Peso de reação** — a abundância do produto de uma reação dividida pela abundância do seu reagente numa injeção; o que a análise de vias compara entre classes.

**Peso isotópico** — 0 para o íon monoisotópico, 1 para M+1, e assim por diante; uma feature marcada 1 ou mais é isótopo de outra.

**Pico** — um pico cromatográfico de uma injeção a um m/z.

**Porcentagem de preenchimento** — a fração de injeções em que o pico de uma feature foi detectado em vez de preenchido.

**PQN** — normalização por quociente probabilístico: cada injeção escalada pela mediana das suas razões feature a feature com um perfil de referência; a correção habitual para um efeito de diluição.

**Preenchimento de lacunas** — integrar o cromatograma no lugar de uma feature numa injeção onde nenhum pico foi detectado.

**Q²** — a fração da pertença de classe que um modelo prevê para injeções em que não foi ajustado.

**QC** — uma injeção de controle de qualidade agrupada, o mesmo material toda vez; o tipo que a correção de deriva lê.

**R²Y** — a fração da pertença de classe que um modelo reproduz nas suas próprias injeções.

**Representativa** — a injeção cujo pico pontuou melhor para uma feature; o seu espectro e nome são os da feature.

**Revisada** — uma feature com qualquer marcação, ou marcada como tal pelo revisor.

**Sobre-representação (ORA)** — perguntar se as features que mudaram caem num conjunto mais vezes do que o acaso as poria lá. Veja [[one-factor-analysis#Enriquecimento de lipídios]].

**Somente-componente ortogonal** — em OPLS-DA, a variação retirada porque não separa as classes.

**S-plot** — covariância contra correlação de toda feature com o componente preditivo de um modelo OPLS-DA.

**Teste de permutação** — reajustar um modelo com os rótulos de classe embaralhados muitas vezes para ver com que frequência o acaso faz tão bem.

**Teste hipergeométrico** — o teste de sobre-representação: a chance de tirar pelo menos este número de membros de um conjunto entre as features significativas, quando são tiradas das testadas sem reposição.

**Teste t de Welch** — o teste t de dois grupos que não assume que as duas classes se dispersam da mesma forma; a comparação padrão.

**Tipo** — o que uma injeção é fisicamente: Sample, Blank, QC ou Standard.

**Tipo de aquisição** — como o MS/MS foi adquirido: DDA (dependente de dados, um precursor por varredura), SWATH (independente de dados, janelas de isolamento fixas) ou AIF (fragmentação de todos os íons). Decide que varreduras de produto pertencem a um pico. Veja [[processing#Tipos de aquisição]].

**Two-way ANOVA** — a análise de variância com dois fatores: o efeito de cada um, e a sua interação, testados numa feature. Veja [[two-factor-analysis]].

**Varredura de survey** — uma varredura de MS1.

**VIP** — variable importance in projection; quanto da separação de um modelo assenta numa feature.

**Volcano plot** — o log2 fold change de toda feature na horizontal contra −log10 do seu p na vertical, de modo que as features que mudaram muito e com confiança ficam nos cantos superiores.

**XIC / EIC** — um cromatograma de íon extraído: intensidade dentro de uma janela de massa contra tempo de retenção.

**Z-score** — um valor menos a sua média, dividido pelo seu desvio padrão; o que uma linha de mapa de calor padronizada mostra.
