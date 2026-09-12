---
title: Parâmetros do método
section: Reference
order: 42
summary: Todo parâmetro do arquivo de método — a sua chave, o seu padrão, e o que faz ao resultado — seção por seção.
---

# Parâmetros do método

O método é um arquivo de texto simples de linhas `Key: value`, o formato que o console do MS-DIAL
lê, editado como formulário na [[method-workspace]]. Esta página lista toda chave que o formulário
modela, com o seu padrão para LC-MS e o que mudá-la faz. As chaves não distinguem maiúsculas. As
chaves que o formulário não conhece são mantidas literalmente sob `#Other`.

Os padrões são os do MS-DIAL para uma corrida de metabolômica LC-MS/MS DDA em modo positivo num
instrumento de alta resolução. Uma exportação de parâmetros do Windows é convertida para este
formato por `tools/msdial_param_to_method.py` (veja [[command-line]]), de modo que uma corrida
Windows pode ser reproduzida exatamente.

## Data type

| Chave | Padrão | Significado |
| --- | --- | --- |
| `MS1 data type` | `Centroid` | se as varreduras de survey são centroidadas ou de perfil. Os arquivos de fabricante convertidos e os `.wiff` lidos nativamente são centroidados; um mzML de perfil precisa de `Profile` |
| `MS2 data type` | `Centroid` | o mesmo para as varreduras de produto |
| `Ion mode` | `Positive` | `Positive` ou `Negative`; decide os adutos e a polaridade da biblioteca |
| `Target omics` | `Metabolomics` | `Metabolomics` ou `Lipidomics`; lipidômica liga a validação de nomes de lipídios a partir dos fragmentos do MS-DIAL, e numa biblioteca de lipídios vale muito — as mesmas oito injeções de fígado, todo o resto igual, deram 79 features nomeadas pelo seu MS/MS como metabolômica e 216 como lipidômica |
| `Acquisition type` | `DDA` | o padrão para arquivos novos: `DDA`, `SWATH` ou `AIF`; cada arquivo pode sobrepor-se a ele na área Samples |
| `Machine category` | `LCMS` | `LCMS` ou `GCMS`; escrito a partir do modo, não editado |

## Data collection

| Chave | Padrão | Significado |
| --- | --- | --- |
| `Retention time begin` / `end` | `0` / `100` | minutos; as varreduras fora são ignoradas |
| `MS1 mass range begin` / `end` | `50` / `1500` | m/z; os picos de survey fora são ignorados |
| `MS2 mass range begin` / `end` | `50` / `1500` | m/z, para as varreduras de produto |

## Centroid parameters

| Chave | Padrão | Significado |
| --- | --- | --- |
| `MS1 tolerance for centroid` | `0.01` | Da; a largura com que um pico de survey é centroidado e extraído — também a tolerância a que a grade de revisão desenha os cromatogramas |
| `MS2 tolerance for centroid` | `0.025` | Da, para as varreduras de produto |
| `Mass accuracy` | `0.01` | escrito igual à tolerância de centroide de MS1 |

## Peak detection

| Chave | Padrão | Significado |
| --- | --- | --- |
| `Smoothing method` | `LinearWeightedMovingAverage` | também `SimpleMovingAverage`, `SavitzkyGolayFilter`, `BinomialFilter`, `LowessFilter`, `LoessFilter` |
| `Smoothing level` | `3` | a meia-largura da janela de suavização, em varreduras |
| `Minimum peak width` | `5` | varreduras; picos mais estreitos são descartados |
| `Minimum peak height` | `1000` | intensidade absoluta; a maior alavanca sobre quantos picos uma corrida encontra. As alturas de pico dependem da centroidização, e é por isso que o centroidador da SCIEX importa (veja [[raw-data-formats#SCIEX .wiff]]) |
| `Mass slice width` | `0.1` | Da; a largura das fatias de m/z em que os cromatogramas são extraídos |
| `Max charge number` | `2` | o estado de carga mais alto considerado ao agrupar isótopos |
| `Searched adduct ions` | `[M+H]+,[M+Na]+,[M+NH4]+` | separados por vírgula; os adutos em que a corrida agrupa os picos e com que anota |

## Deconvolution

| Chave | Padrão | Significado |
| --- | --- | --- |
| `Sigma window value` | `0.5` | quão larga, em larguras de pico, o MS2Dec modela uma contribuição coeluída; maior tolera cromatografia pior |
| `Amplitude cut off` | `0` | intensidade absoluta abaixo da qual os íons-produto são descartados |
| `Keep isotope range` | `0.5` | Da; íons-produto até esta distância acima do precursor são mantidos como isótopos |
| `Exclude after precursor` | `True` | descartar íons-produto mais pesados do que o precursor |

## Identification

| Chave | Padrão | Significado |
| --- | --- | --- |
| `MSP file path` | | a biblioteca espectral; `.msp`, `.msp2`, `.lbm2`. Vazio deixa toda feature desconhecida |
| `Text DB file path` | | uma biblioteca de texto opcional de tempo de retenção/m/z |
| `RT tolerance for MSP-based annotation` | `0.5` | minutos, quando o tempo de retenção é usado |
| `Mass range begin` / `end for MSP-based annotation` | `0` / `2000` | os registros fora não são buscados |
| `Relative` / `Absolute amplitude cutoff for MSP-based annotation` | `0` / `0` | os íons-produto abaixo destes são ignorados ao pontuar |
| `Weighted dot product cutoff` | `0.4` | um casamento precisa de pelo menos isto |
| `Simple dot product cutoff` | `0.4` | |
| `Reverse dot product cutoff` | `0.4` | |
| `Matched peaks percentage cutoff` | `0.2` | pelo menos esta fração dos picos da referência encontrada |
| `Minimum spectrum match` | `1` | pelo menos este número de picos da referência encontrados |
| `Total score cutoff` | `60` | por cento; abaixo dele um casamento é reportado como `low score:` |
| `MS1 tolerance for MSP-based annotation` | `0.01` | Da; quão longe um precursor da biblioteca pode ficar do m/z do pico |
| `MS2 tolerance for MSP-based annotation` | `0.05` | Da; quão longe um fragmento pode ficar do da referência |
| `Use retention information for MSP-based annotation scoring` | `True` | somar a similaridade de retenção à pontuação quando a biblioteca traz tempos de retenção |
| `… filtering` | `False` | rejeitar de imediato os registros fora da tolerância de RT |
| `Only report top hit for MSP-based annotation` | `True` | guardar um candidato por pico; `False` guarda os outros para a aba Candidates |

As próprias pontuações estão explicadas em [[evidence-panels#MS/MS]] e [[annotation]].

## GC-MS

Presente só no modo GC-MS.

| Chave | Padrão | Significado |
| --- | --- | --- |
| `Retention type` | `RT` | `RT` ou `RI`; se a identificação usa tempo de retenção ou índice de retenção |
| `RI compound type` | `Alkanes` | `Alkanes` ou `Fames`; em que o índice é calibrado |
| `Alignment index type` | `RT` | `RT` ou `RI`; o que o alinhamento usa |
| `RI index file pathes` | | uma tabela separada por tabulações: caminho do arquivo bruto, caminho do dicionário de RI — uma linha por arquivo; o próprio dicionário tem duas colunas, número de carbonos e tempo de retenção. Obrigatório, para todo arquivo, quando `RI` é usado |

## Alignment

| Chave | Padrão | Significado |
| --- | --- | --- |
| `Alignment reference file ID` | `0` | a injeção a que as outras são alinhadas, pela sua posição no lote (0 é a primeira) |
| `Retention time tolerance for alignment` | `0.1` | minutos; quão afastados dois picos podem ficar e ainda ser uma feature — e cinco vezes isto é a janela esperada que a grade de revisão sombreia |
| `MS1 tolerance for alignment` | `0.015` | Da |
| `Retention time factor for alignment` | `0.5` | o peso do tempo de retenção na pontuação de casamento |
| `MS1 factor for alignment` | `0.5` | o peso do m/z |
| `Peak count filter` | `0` | por cento; uma feature detectada em menos injeções do que isto é descartada |
| `N percent detected in one group` | `0` | por cento; uma feature tem de ser detectada em pelo menos esta fração de uma classe |
| `Gap filling by compulsion` | `True` | integrar o cromatograma onde nenhum pico foi detectado, e marcar o valor como preenchido |
| `Together with alignment` | `True` | alinhar depois da detecção de picos; `False` para depois dos resultados por arquivo |

## Export and process

| Chave | Padrão | Significado |
| --- | --- | --- |
| `Is height matrix export` | `True` | gravar a matriz de QA `.qa.tsv` |
| `Number of threads` | os núcleos da máquina, pelo menos 2 | os arquivos são processados em paralelo, metade deste número de cada vez |

## Um arquivo completo

```
#Data type
MS1 data type: Centroid
MS2 data type: Centroid
Ion mode: Positive
Target omics: Metabolomics
Acquisition type: DDA
Machine category: LCMS

#Data collection parameters
Retention time begin: 0.0
Retention time end: 100.0
MS1 mass range begin: 50.0
MS1 mass range end: 1500.0
MS2 mass range begin: 50.0
MS2 mass range end: 1500.0

#Centroid parameters
MS1 tolerance for centroid: 0.01
MS2 tolerance for centroid: 0.025

#Peak detection parameters
Smoothing method: LinearWeightedMovingAverage
Smoothing level: 3
Minimum peak width: 5
Minimum peak height: 1000
Mass slice width: 0.1
Mass accuracy: 0.01
Max charge number: 2

#Deconvolution parameters
Sigma window value: 0.5
Amplitude cut off: 0.0
Keep isotope range: 0.5
Exclude after precursor: True

#Adduct list
Searched adduct ions: [M+H]+,[M+Na]+,[M+NH4]+

#MSP file and MS/MS identification setting
MSP file path: /data/libraries/Pos_GLDB_v0-1-0-alpha.msp
RT tolerance for MSP-based annotation: 0.5
Mass range begin for MSP-based annotation: 0.0
Mass range end for MSP-based annotation: 2000.0
Relative amplitude cutoff for MSP-based annotation: 0.0
Absolute amplitude cutoff for MSP-based annotation: 0.0
Weighted dot product cutoff for MSP-based annotation: 0.4
Simple dot product cutoff for MSP-based annotation: 0.4
Reverse dot product cutoff for MSP-based annotation: 0.4
Matched peaks percentage cutoff for MSP-based annotation: 0.2
Minimum spectrum match for MSP-based annotation: 1.0
Total score cutoff for MSP-based annotation: 60.0
MS1 tolerance for MSP-based annotation: 0.01
MS2 tolerance for MSP-based annotation: 0.05
Use retention information for MSP-based annotation scoring: True
Use retention information for MSP-based annotation filtering: False
Only report top hit for MSP-based annotation: True

#Alignment parameters setting
Alignment reference file ID: 0
Retention time tolerance for alignment: 0.1
MS1 tolerance for alignment: 0.015
Retention time factor for alignment: 0.5
MS1 factor for alignment: 0.5
Peak count filter: 0.0
N percent detected in one group: 0.0
Gap filling by compulsion: True
Together with alignment: True

#Export
Is height matrix export: True

#Process
Number of threads: 8
```
