---
title: Processar um lote
section: Workspaces
order: 13
summary: O que acontece quando se pressiona Process — as etapas, o progresso, o que é gravado, quanto demora, e o que pode pará-lo.
---

# Processar um lote

**Process batch** — `⌘R`, a barra de ferramentas da área Analytics, ou o último passo do
assistente — corre o fluxo completo do MS-DIAL sobre o lote: leitura dos arquivos brutos, detecção
de picos, deconvolução de MS/MS, anotação contra a biblioteca, alinhamento entre as injeções,
preenchimento de lacunas e as exportações. Corre em segundo plano; a janela continua utilizável e
muda para Analytics.

## Antes de começar

A corrida recusa, com uma mensagem na barra de status, quando o lote está vazio (muda para
Samples), quando já há uma corrida em curso, ou quando o método nomeia uma biblioteca MSP que não
existe (muda para Method). Quando não há pasta de saída definida, uma é escolhida: `results` ao
lado do arquivo de projeto, ou `milx_results` ao lado do primeiro arquivo bruto num lote sem
projeto.

## As etapas

A faixa de progresso nomeia-as; o log (`⌘L`) tem o detalhe.

| Etapa | O que acontece |
| --- | --- |
| **Setup** | a pasta de saída é criada; o método é gravado nela como `milx_method.txt`; o passo de correção de tempo de retenção é preparado |
| **Converting** | todo arquivo de fabricante que a aplicação não lê nativamente passa pelo msconvert para mzML, uma vez, e o mzML é guardado para a próxima vez; veja [[raw-data-formats]] |
| **Setup** — carregar bibliotecas | as bibliotecas MSP, LBM e de texto nomeadas no método são lidas; o log diz quantos registros cada uma tem |
| **Processing** | cada injeção: detecção de picos, deconvolução, anotação. Os arquivos correm em paralelo, metade de cada vez do número de threads do método; a faixa mostra o arquivo e a sua porcentagem |
| **Export** | tabelas de picos por arquivo (`.mdpeak`) e espectros de MS/MS (`.mdmsp`) |
| **Alignment** | os picos de toda injeção são casados em features contra o arquivo de referência, preenchidos e refinados; depois a tabela de alinhamento, a matriz de QA, os espectros do alinhamento e o mzTab-M são gravados |
| **Project** | o projeto MS-DIAL (`.mdproject` + `.mddata`) é salvo na pasta de resultados |
| **Done** | o resultado é carregado em Analytics e Statistics, e o projeto MIL-X é salvo se tiver um caminho |

Todo arquivo que as etapas gravam está listado em [[projects-and-files#A pasta de resultados]].

## Tipos de aquisição

Cada injeção tem um tipo de aquisição — DDA, SWATH ou AIF — definido na área Samples, com o padrão
do método. Ele decide como os espectros de produto de um pico são reunidos: com **DDA** um
espectro de produto pertence a um pico só quando o seu precursor está dentro da tolerância de
centroide do m/z do pico; com **SWATH** e **AIF** tudo o que está dentro da janela de isolamento
é tomado. Uma aquisição IDA da SCIEX é DDA. Vale saber ao comparar com um projeto Windows: o
MS-DIAL 5.5 marcou os arquivos IDA do conjunto de validação como SWATH, que é a regra mais folgada;
correr os mesmos dados como DDA atribui MS/MS a menos picos (897 contra 1 218 no primeiro arquivo
desse conjunto).

## Quanto demora

A primeira leitura de um arquivo de fabricante é a parte lenta — cerca de 25 segundos para um
`.wiff` de 60 MB do ZenoTOF pela biblioteca da SCIEX — seguida da detecção de picos a um custo
semelhante. Oito injeções dessas processam e alinham em alguns minutos num portátil Apple
Silicon. Reabrir o projeto depois é imediato, porque as varreduras de survey ficam em cache após a
primeira leitura; veja [[caches-and-storage]]. Uma biblioteca MSP grande (a biblioteca de
lipídios de 224 MB usada na validação) demora a carregar e é carregada uma vez por corrida.

## Cancelar

**Cancel** na faixa, ou **Process ▸ Cancel run**, para no próximo ponto de verificação: entre
arquivos, entre etapas. O que foi gravado fica na pasta; nada é carregado.

## Quando falha

A barra de status diz `Run failed: …`, o log abre sozinho com o erro completo, e nada é carregado.
As causas habituais estão em [[troubleshooting]]: um arquivo bruto que não pôde ser lido, um
caminho de biblioteca que mudou, msconvert não encontrado, um valor de método que não pôde ser
lido.

## Números a esperar

Na corrida de lipidômica de oito injeções do ZenoTOF 7600 usada para validar o port, contra o
MS-DIAL 5.5 no Windows com os mesmos parâmetros: 9 981 picos contra 9 852 nas amostras; 1 889 dos
1 906 picos da primeira amostra casaram um a um com uma razão de altura de 1,000; 1 611 features
alinhadas contra 1 731. Os espectros de MS/MS deconvoluídos dos picos partilhados casam um a um.
Onde os nomes diferem, a causa está na construção da biblioteca, não no processamento; veja
[[annotation#Por que duas corridas podem discordar]].
