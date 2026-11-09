---
title: A linha de comando e as ferramentas
section: Reference
order: 44
summary: milx, a linha de comando que o MIL-X partilha com o MIL-Q — resumos primeiro, JSON na saída, um servidor de fila de arquivos para um agente — mais o console do MS-DIAL, os scripts e as ferramentas de diagnóstico.
---

# A linha de comando e as ferramentas

## milx

O MIL-X e o MIL-Q implementam um só contrato de linha de comando (`docs/CLI-CONTRACT.md`, versão
`mil-cli/1`, byte a byte idêntico nos dois repositórios): os mesmos subcomandos, as mesmas opções e
os mesmos esquemas JSON, de modo que um script ou um agente pode chamar `milq` ou `milx` e
interpretar a resposta da mesma forma. O binário é publicado por `scripts/build-cli.sh` em
`dist/milx-cli-<rid>/milx`, autocontido, com o plugin da SCIEX ao lado; `milx-cli lcms …` na mesma
pasta é o próprio console do MS-DIAL, inalterado.

O contrato foi escrito para um leitor que paga por cada linha que lê, e três regras seguem disso.
**Resumos primeiro**: todo subcomando que poderia imprimir milhares de linhas tem uma forma
`--summary`, o padrão, que responde à pergunta de sempre em poucas linhas — ápice, altura, área,
pontos sobre o pico, escore de coeluição — e o traço completo só vem com `--trace`. **Só legível
por máquina**: JSON em stdout (`--format json`) ou TSV (`--format tsv`), nada mais em stdout,
diagnósticos em stderr, e com `--quiet` nada em stderr além dos erros. **Determinístico**: a mesma
entrada dá os mesmos bytes, com ponto como separador decimal seja qual for o locale, m/z com quatro
casas (seis para valores teóricos), tempos de retenção em minutos com quatro, intensidades como o
leitor as relata. Todo documento começa com `"schema": "mil-cli/1"`, `"tool": "milx"`, a versão, o
comando e a entrada (caminho absoluto, índice da amostra). Um erro é JSON em stderr —
`{"schema":"mil-cli/1","error":"unreadable","message":"…"}` — com saída 2 para argumentos errados,
3 para um arquivo que não pode ser lido (um `.wiff` sem o seu `.wiff.scan`), 4 para nada encontrado
(só com `--require`; um resultado vazio não é erro de outro modo), 5 para uma falha interna.

| Subcomando | Responde | Opções principais |
| --- | --- | --- |
| `selftest [FILES…]` | o que funciona nesta máquina: plataforma, runtime, se o SDK da SCIEX carregou, se o msconvert foi encontrado, e para cada arquivo se ele lê, com quantos espectros e em quanto tempo | |
| `info FILE` | as amostras do arquivo e os seus canais: índice, tipo (`ms1`, `ida-slot`, `dda-events`, `swath`, `product`), rótulo, contagem de varreduras, intervalo de massa, energia de colisão, polaridade | `--sample N\|NAME` |
| `scans FILE` | a tabela de varreduras de uma fonte: varredura, ciclo, RT, nível de MS, `parent_mz` — o precursor que esta varredura de fato isolou —, janela de isolamento, CE, TIC, pontos | `--source`, `--rt A:B`, `--parent MZ[:TOL]`, `--ms-level N`, `--count` |
| `xic FILE` | um resumo por m/z: RT do ápice, altura do ápice, área, FWHM, pontos sobre o pico, S/N; contra um íon `--reference MZ@SOURCE`, o r de Pearson sobre o pico de referência, o ΔRT dos ápices e a razão de alturas | `--mz LIST` (vírgulas ou espaços), `--tol 0.01`, `--unit Da\|ppm`, `--source`, `--rt A:B`, `--smooth N`, `--trace` |
| `spectrum FILE` | um espectro como lista de picos — uma varredura, ou a média sobre um intervalo de tempo — com fundos subtraídos, as varreduras usadas e o seu precursor | `--scan N` ou `--rt A:B`, `--subtract A:B` (repetível), `--centroid vendor\|local\|none`, `--top N`, `--min-rel 0.01`, `--msp PATH` |
| `isotopes FILE` | o envelope medido — M, M+1, M+2… — contra a teoria e a conferência do espaçamento | `--mz`, `--charge`, `--rt A:B`, `--formula` |
| `batch-xic PATHS…` | o resumo do `xic` sobre muitos arquivos, uma linha por arquivo, amostra e m/z, com os achados da conferência de pasta como pulos | como `xic`, mais `--rt-window C±W` |
| `convert FILE` | mzML que guarda o precursor de cada varredura | `--out`, `--centroid` |
| `export --project P` | as exportações da revisão sem a janela: `--reviewed OUT` (a tabela marcada), `--matrix OUT` (alturas e áreas), `--milq OUT` (a lista de componentes) | `--areas`, `--annotated-only` |
| `serve` | mantém arquivos abertos e responde a pedidos: `--stdio` para linhas JSON, `--inbox DIR` para a pasta abaixo | `--allow-root DIR` (repetível), `--max-jobs 1`, `--idle-exit MIN` |
| `lcms`, `gcms`, … | os fluxos do próprio MS-DIAL, corridos no mesmo processo (veja abaixo) | `--acquisition-type`, `--library`, `--summary` |

Uma **fonte** diz de onde um cromatograma ou um espectro é lido: `ms1`, o canal de survey;
`channel:N`, um canal pelo seu índice em `info`; `ms2-all`, toda varredura MS2 de todo canal,
fundidas por tempo de retenção; e `gated:MZ[:TOL]` — só as varreduras MS2 cujo próprio precursor
está a TOL (padrão 0,05 Da) de MZ, de qualquer slot IDA ou evento DDA, fundidas por RT. O portão é o
mesmo mecanismo da fonte *Gated by precursor* do Explorer ([[explorer-workspace#Os painéis]]), e é o
que o cromatograma de um íon-produto precisa num arquivo dependente de dados, onde os espectros de
um precursor caem no slot que estava livre em cada ciclo.

O resumo do `xic` declara as suas definições no documento, porque o MIL-Q calcula as mesmas: o
ápice é o ponto mais alto na janela; os limites caminham para fora dele até o primeiro ponto em ou
abaixo de 5 % do ápice, ou até uma subida de um décimo do ápice depois de um vale; a área é o
trapézio entre os limites em intensidade × minutos; a FWHM interpola os dois cruzamentos de meia
altura; o S/N é o ápice sobre a estimativa automática de ruído do MIL-Q — o desvio absoluto mediano
das diferenças ponto a ponto escalado a um desvio padrão, o desvio padrão da metade inferior quando
mais da metade das diferenças é zero — e é nulo quando nada pode ser medido, como num traço de
zeros exatos ([[evidence-panels#Sinal-ruído sobre uma região de ruído]] diz por quê).

```bash
milx info run.wiff --sample 2
milx scans run.wiff --source ms2-all --count
milx xic run.wiff --mz "760.5851, 184.0733" --tol 0.01
milx xic run.wiff --mz 184.0733 --source gated:760.5851 --reference 760.5851@ms1
milx spectrum run.wiff --source gated:760.5851 --rt 11.3:11.5 --subtract 10:10.5 --top 20 --msp pc.msp
milx isotopes run.wiff --mz 760.5851 --rt 11.3:11.5 --formula C42H82NO8P
milx batch-xic ~/data/batch --mz 760.5851 --rt-window "11.4±0.3" --format tsv
milx convert run.wiff --out run.mzML
milx export --project results/ --reviewed reviewed.tsv --matrix matrix.tsv --milq components.csv
```

### O que mediu

Numa aquisição IDA de um ZenoTOF 7600 (um `.wiff.scan` de 64 MB, 7 906 espectros em 25
experimentos, 490 ciclos), lida nativamente num Mac com Apple Silicon:

| Chamada | Tempo | Memória de pico |
| --- | --- | --- |
| `info`, a frio | 27,1 s — a leitura do fabricante com a centroidização da própria SCIEX | 343 MB |
| `info`, a quente (`--cache`) | 0,16 s | 208 MB |
| `xic --mz 760.5851`, a frio | 29,3 s | 314 MB |
| `scans --source ms2-all --count` | 7 416 varreduras MS2, 7 301 precursores distintos | |

A leitura é o custo, e é pago uma vez no modo `serve`, em que os arquivos ficam abertos entre
pedidos: o mesmo `xic` pela caixa de entrada, arquivo já aberto, respondido em 0,26 s. O arquivo
auxiliar `--cache DIR` (o cache de espectros da própria aplicação de desktop) responde `info` e
`scans` sem a biblioteca do fabricante na segunda chamada; as intensidades de `xic` e `spectrum`
vêm sempre do leitor, portanto um documento tem os mesmos bytes a frio e a quente.

No estudo DDA sintético (`tools/make_synthetic_mzml.py`, 563 espectros): `info` em 0,17 s;
`convert` grava 2,9 MB de mzML, e `scans` sobre o resultado dá o mesmo `parent_mz` para todas as
62 varreduras MS2 e o mesmo ápice e área de `xic` até o último dígito; `lcms --summary` sobre as
três amostras leva 2,6 s e relata 35, 40 e 32 picos, 12 anotados em cada, 18 features alinhadas.
`export` sobre o projeto de fígado com oito injeções grava 2 235 features de três formas.

### Os fluxos do MS-DIAL, com um resumo

`milx lcms -i IN -o OUT -m METHOD [-p]` corre o console do MS-DIAL no mesmo processo, exatamente
como `milx-cli lcms`, mais três coisas de que um agente precisa. `--acquisition-type DDA|SWATH|AIF`
e `--library X.msp` sobrescrevem o arquivo de método — os projetos do MS-DIAL 5.5 dos dados IDA
deste usuário estavam marcados SWATH, veja [[raw-data-formats#SCIEX .wiff]] — e o método de fato
usado é gravado ao lado dos resultados como `milx_method.txt`. `--summary` substitui o log do
console (guardado em `milx-run.log`) por um JSON: por arquivo, picos, MS/MS atribuídos e anotados;
as features do alinhamento, MS/MS atribuídos e anotados; e os caminhos de toda exportação. `gcms`,
`dims`, `imms`, `lcimms`, `msn` e `eic` passam inalterados.

### serve --inbox: a fila de arquivos

Um agente que trabalha numa máquina virtual Linux vê as pastas do Mac mas não os seus programas, e
não consegue digitar num terminal. Então o Mac corre, uma vez, num terminal:

```bash
milx serve --inbox ~/Documents/mil-jobs --allow-root ~/Documents/MIL-data
```

e o agente escreve arquivos de pedido. A pasta recebe `inbox/`, `running/`, `outbox/`, `failed/`,
`log.jsonl`, `STATUS.json` e, quando é hora de parar, um arquivo `STOP`. Um pedido é um arquivo
`inbox/<id>.json`, escrito atomicamente (escreva `<id>.json.tmp`, depois renomeie):

```json
{"schema": "mil-cli/1", "id": "0042", "command": "xic",
 "args": {"file": "/Users/me/Documents/MIL-data/run.wiff", "mz": "760.5851, 184.0733", "tol": 0.01},
 "format": "json", "timeout_s": 600}
```

Os args são as opções do comando sem traços; um `"batch": [ … ]` corre vários pedidos em ordem
num só arquivo. O servidor pega os pedidos por ordem de nome — um observador do sistema de arquivos,
com uma sondagem a cada segundo por trás — move o arquivo para `running/`, executa-o no mesmo
processo com o mesmo código que a linha de comando corre (sem shell, sem subprocesso, sem eval),
grava `outbox/<id>.json` atomicamente e guarda o pedido ao lado como `outbox/<id>.request.json`,
ou põe os dois em `failed/` com o documento de erro. A resposta é byte a byte o que `milx xic …`
teria impresso; um teste confere. Um traço, um MSP ou um TSV vai para um arquivo sob
`outbox/<id>/`, e a resposta lista-o em `files`.

É uma lista de permissão, não um shell: só os subcomandos do contrato e o fluxo do MS-DIAL são
aceitos, qualquer outra coisa falha com `not_allowed`. Todo caminho num pedido — entradas e saídas
— tem de resolver, links simbólicos seguidos, dentro de um `--allow-root` ou dentro da pasta de
entrada, ou o pedido falha com `path_outside_roots`; nada fora de `outbox/` é jamais sobrescrito. Um
trabalho corre por vez por padrão (`--max-jobs 1`), para que um trabalho dimensionado para uma
máquina pequena não esfomeie esta; `timeout_s` (padrão 1800) encerra um trabalho desgovernado com
`timeout`. `STATUS.json` é reescrito a cada mudança — versão, pid, início, o trabalho atual e o seu
tempo decorrido, o tamanho da fila, o último erro — e `log.jsonl` recebe uma linha por trabalho:
id, comando, args, início, fim, saída, os caminhos de saída e a memória residente de pico; nunca
guarda conteúdo de arquivo. Criar `STOP` (ou Ctrl-C, ou `--idle-exit MIN` sem nada a fazer) termina
o trabalho atual e sai, removendo `STOP`; um servidor que encontra arquivos em `running/` ao
iniciar move-os para `failed/` como `interrupted`.

Para que o servidor inicie com a sessão, um agente do `launchd` faz isso; salve isto como
`~/Library/LaunchAgents/io.github.pedronobrez.milx.serve.plist` e `launchctl load` nele:

```xml
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0"><dict>
  <key>Label</key><string>io.github.pedronobrez.milx.serve</string>
  <key>ProgramArguments</key><array>
    <string>/Applications/MIL-X.app/Contents/MacOS/milx</string>
    <string>serve</string><string>--inbox</string><string>/Users/me/Documents/mil-jobs</string>
    <string>--allow-root</string><string>/Users/me/Documents/MIL-data</string><string>--quiet</string>
  </array>
  <key>RunAtLoad</key><true/>
  <key>KeepAlive</key><true/>
  <key>StandardErrorPath</key><string>/Users/me/Documents/mil-jobs/serve.err</string>
</dict></plist>
```

`serve --stdio` é o mesmo servidor sobre um pipe: um pedido por linha em stdin, um documento por
linha em stdout, para um programa que lança o `milx` ele mesmo.

### O alvo Linux

A segunda máquina do contrato é uma máquina virtual Ubuntu 22.04 aarch64 com cerca de 3 GB de
memória e sem .NET: `scripts/build-cli.sh linux-arm64` publica uma pasta autocontida para ela, nada
a instalar. Se `.wiff` lê lá é decidido por `milx selftest run.wiff` naquela máquina — o plugin da
SCIEX corre em .NET 8 em linux-arm64 em princípio, e não foi verificado naquela máquina a partir
daqui; mzML lê em todo lugar, e `convert` no Mac é o caminho que faz um `.wiff` viajar. Com o
servidor de caixa de entrada no Mac, a máquina Linux não precisa de nenhum dos dois.

### O teste do contrato

`tests/MilX.Cli.Tests` corre todo subcomando sobre o estudo sintético e sobre uma variante dele ao
estilo IDA (`make_synthetic_mzml.py --ida 4`: quatro slots dependentes por ciclo, preenchidos por
ordem, sem exclusão, de modo que um precursor se move entre slots), comanda o servidor de caixa de
entrada com os quatro pedidos do contrato — um válido, um com caminho fora das raízes, um com um
comando que a lista de permissão recusa, um que estoura o tempo — e compara `milx` com `milq` em
`info`, `scans`, `xic` (ms1 e com portão) e `spectrum` quando `MILQ_CLI` nomeia o executável do
MIL-Q (RT exato, m/z 1e-4, intensidade relativa 1e-6); `MILX_TEST_WIFF` acrescenta uma aquisição
real.

## milx-cli

O console do próprio MS-DIAL (`MSDIALCUI`), construído com a camada de dados brutos do MIL-X e
publicado ao lado do `milx`. Corre o mesmo motor que a aplicação de desktop, sem a janela.

```bash
milx-cli lcms -i /data/run1 -o /data/run1/out -m method.txt -p
```

| Opção | Significado |
| --- | --- |
| `lcms`, `gcms`, `dims`, `imms`, `lcimms`, `msn`, `eic` | o fluxo; a aplicação de desktop cobre `lcms` e `gcms` |
| `-i` | uma pasta (todo arquivo bruto nela), um arquivo, ou um CSV listando arquivos com a sua classe e tipo |
| `-o` | a pasta de saída |
| `-m` | o arquivo de método; veja [[method-parameters]] |
| `-p` | gravar também um `.mdproject`, que a aplicação de desktop e o MS-DIAL no Windows abrem |
| `--version` | a versão do MS-DIAL |

O lançador `milx-cli` força a cultura invariante antes de começar; use-o em vez de
`MSDIALCUI` diretamente. Os arquivos de fabricante são convertidos e os `.wiff` são lidos
nativamente exatamente como na aplicação, com o plugin em `plugins/sciex` ao lado do binário.

## Scripts

Todos sob `milx/scripts/`; veja [[building-and-testing]] para os de construção.

| Script | O que faz |
| --- | --- |
| `smoke-ui.py` | comanda a aplicação instalada e verifica que funciona: `--project` abre um projeto processado e verifica a janela; `--process FOLDER --library X.msp` constrói um projeto a partir dos arquivos brutos, processa-o, reintegra um pico, salva, exporta, abre o manual |
| `ui-drive.sh` | as mesmas primitivas uma de cada vez — `front`, `click X Y`, `key CODE cmd`, `shot PATH`, `where` — para uma captura de tela ou uma olhada |
| `build-manual.py` | constrói o PDF e o HTML deste manual a partir de `docs/manual`; `--lang pt` constrói a edição em português a partir de `docs/manual/pt` |
| `make-app-bundle.sh` | publica e empacota `dist/MIL-X.app` |
| `build-gui.sh`, `build-cli.sh` | publicam a aplicação, e o `milx` com o console (`build-cli.sh linux-arm64` para a máquina Linux) |
| `test.sh` | toda suíte de testes mais as corridas sintéticas ponta a ponta |
| `setup-macos.sh` | o SDK do .NET, sem sudo |
| `fetch-sciex-assemblies.sh` | o SDK da SCIEX para `vendor/sciex`, para o leitor nativo de `.wiff` |

## Ferramentas

Sob `milx/tools/`, corridas com `dotnet run --project tools/<name> -- …`:

| Ferramenta | O que faz |
| --- | --- |
| `msdial_param_to_method.py <param.txt> --out method.txt [--msp lib.msp]` | transforma uma exportação de parâmetros do MS-DIAL do Windows num arquivo de método, chave a chave, para que uma corrida possa ser reproduzida |
| `make_synthetic_mzml.py --out DIR [--samples N] [--mode gcms] [--ida SLOTS]` | um conjunto de dados sintético LC-MS/MS DDA (ou GC-MS EI) com a sua biblioteca e método, para os testes ponta a ponta; `--ida` dispõe as varreduras MS2 como slots IDA, vários precursores partilhando um slot |
| `ResultCompare <reference> <test> [rtTol] [mzTol] [out.tsv]` | casa dois resultados pico a pico e feature a feature e relata as razões; `--check-library <project> <lib.msp>` pergunta se uma biblioteca poderia ter produzido as anotações de uma corrida; `--dump-params <project>` imprime os parâmetros de um projeto; `--name-detail` lista as discordâncias de nome |
| `RawDump <file> [--full] [--max N]` | imprime o que a camada de dados brutos lê de um arquivo: espectros, precursores, tempos — a verificação de paridade aberto contra fechado |
| `WiffProbe <file.wiff> [cycles]` | imprime como uma aquisição `.wiff` está disposta: experimentos, ciclos, precursores por varredura |
| `make_icon.py` | desenha o ícone da aplicação |
