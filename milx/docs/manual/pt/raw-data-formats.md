---
title: Formatos de dados brutos
section: Data and files
order: 31
summary: Que formatos abrem diretamente, como o .wiff da SCIEX é lido nativamente, como os outros formatos de fabricante passam pelo msconvert, os formatos legados, e o que cada caminho precisa.
---

# Formatos de dados brutos

O MS-DIAL lê dados brutos por uma biblioteca fechada, só para Windows. O MIL-X substitui-a por
uma camada aberta que lê ela própria os formatos abertos, lê o `.wiff` da SCIEX pelo SDK gerenciado
do próprio fabricante, e entrega todo o resto ao msconvert do ProteoWizard uma vez, guardando o
resultado. A etiqueta Format na [[samples-workspace]] diz que caminho um arquivo toma.

| Formato | Caminho | Precisa de |
| --- | --- | --- |
| **mzML**, indexedmzML | lido diretamente | nada |
| **SCIEX `.wiff`** (+ `.wiff.scan`) | leitor nativo | o plugin da SCIEX no pacote (`plugins/sciex`) |
| SCIEX `.wiff2` | leitor nativo, pelo `.wiff` ao lado | o plugin, e o `.wiff` que o SCIEX OS grava com todo `.wiff2`; um `.wiff2` sozinho vai para o msconvert |
| Thermo `.raw` | msconvert | msconvert ou Docker |
| pastas `.d` da Agilent, Bruker | msconvert | msconvert ou Docker; adicionadas com **Add folder…** |
| Shimadzu `.lcd`, `.qgd` | msconvert | msconvert ou Docker |
| MS-DIAL `.ibf`, `.abf`, NetCDF `.cdf`, `.imzML` | o leitor legado do MS-DIAL | a sua dll em `plugins/legacy`; `.abf` é só Windows; `.cdf` também precisa de libnetcdf |

## mzML

Lido pelo leitor de streaming do próprio MIL-X: compressão zlib e MS-Numpress, floats e
inteiros de 32 e 64 bits, grupos de parâmetros referenciáveis, cromatogramas, mobilidade iônica
(tempo de deriva e 1/K0). Dois defeitos do leitor fechado deliberadamente não são reproduzidos: o
último pico de todo espectro é guardado (o leitor fechado descarta-o), e o m/z do pico-base é o m/z
do pico mais intenso. Todo número é lido com ponto como separador decimal seja qual for a
localização da máquina; veja [[troubleshooting#Processamento]].

Mantenha o tipo de dados do método em **Centroid** para arquivos convertidos: o msconvert aplica a
detecção de picos do fabricante durante a conversão. Um mzML de perfil precisa de **Profile** no
método.

## SCIEX .wiff

Lido diretamente pelos assemblies Clearcore2 da SCIEX — o mesmo caminho que o OpenQuant usa — em
cerca de um segundo por arquivo mais o tempo de leitura da própria biblioteca do fabricante. O
arquivo `.wiff.scan` tem de estar ao lado do `.wiff`. Três coisas que o leitor faz e que decidem se
uma corrida casa com o MS-DIAL no Windows:

- **O precursor de um experimento dependente (IDA) é tomado por varredura**, do espectro, não da massa de reserva do experimento, que é a mesma em todo slot dependente.
- **Os espectros de perfil são centroidados pelo detector de picos da própria SCIEX**, que ajusta o ápice e reporta-o cerca de um quarto mais alto do que um máximo local faria. As alturas de pico alimentam o corte de altura mínima, de modo que esta é a diferença entre uma razão de alturas de 0,85 e uma de 1,000 contra o MS-DIAL. `MILX_WIFF_CENTROID` escolhe `sciex` (padrão), `msdial` (o método do máximo local) ou `0` (manter perfil, e pôr o método em Profile).
- **Um arquivo de lote multiamostra vira uma injeção por amostra.** A aplicação cria uma pasta `wiff-samples` ao lado do arquivo com um link por amostra (`name.s1.wiff`, `name.s2.wiff`, …, cada um com o seu link `.wiff.scan`) e lembra que amostra cada um representa; um projeto reaberto depois registra-os de novo pelo sufixo `.sN`. Num compartilhamento que recusa links o arquivo é copiado.

**`.wiff2`.** O SCIEX OS grava toda aquisição duas vezes — um `.wiff2`, o seu contentor mais novo,
e um `.wiff` por compatibilidade — ambos sobre o único `.wiff.scan` que guarda os espectros. O
leitor de `.wiff2` do próprio SDK precisa de uma biblioteca SQLite nativa que não existe para esta
plataforma, de modo que o MIL-X lê um `.wiff2` pelo `.wiff` ao lado: os mesmos espectros, os
mesmos nomes de amostra, o log diz `read through …`. Adicionar uma pasta toma um arquivo por
aquisição — o `.wiff` — e um `.wiff2` escolhido à mão ao lado do seu `.wiff` é trocado pelo `.wiff`
com uma nota na linha de mensagem, de modo que uma injeção nunca é processada duas vezes. Um
`.wiff2` sozinho, sem `.wiff` ao lado, não é reclamado pelo plugin e passa pelo msconvert; a sua
etiqueta diz `wiff · msconvert`.

Sem o plugin — uma construção feita sem o SDK, ou a pasta faltando no pacote — o `.wiff` também cai
no msconvert. E um arquivo que o plugin reclama mas não consegue abrir é entregue ao msconvert em
vez de falhar a corrida; o log diz que leitor desistiu e por quê.

## A ponte msconvert

Os arquivos de fabricante que a aplicação não consegue ler são tratados como mzML que ainda não
foi produzido. No primeiro acesso o arquivo é convertido com `msconvert --mzML --64 --zlib
--filter "peakPicking vendor msLevel=1-"`, o mzML é gravado ao lado da origem (ou na pasta de cache
de conversão de [[settings]]) e lido daí em diante; um `<name>.mzML` pré-convertido mais novo do
que a origem é usado como está. A etapa Converting da faixa de progresso mostra isso acontecendo,
uma vez.

Dois conversores, escolhidos em [[settings]]:

1. **Docker** — qualquer daemon Docker com a imagem oficial `proteowizard/pwiz-skyline-i-agree-to-the-vendor-licenses`, que traz os leitores dos fabricantes sob Wine. A imagem é só x86-64, de modo que em Apple Silicon tem de correr numa máquina virtual emulada: a do Docker Desktop, ou a do colima. A Rosetta não consegue corrê-la — o Wine para de imediato com `rosetta error: invalid gdt selector index` — por isso a máquina tem de ser QEMU. A imagem tem 9 GB descompactada, puxada uma vez. A emulação completa é lenta mas exata: o `small.RAW` de 1,5 MB do próprio ProteoWizard (48 espectros de um Thermo LTQ) converte em cerca de 30 segundos, a maior parte o Wine a arrancar, e o mzML cai ao lado da origem com todo precursor e energia de colisão no lugar.
2. **Um msconvert nativo** no PATH ou no caminho que você der — uma máquina Windows ou uma caixa Linux com a imagem também conseguem converter, e o mzML copiado para cá é reconhecido.

Preparar o colima em Apple Silicon, uma vez:

```bash
brew install colima docker qemu lima-additional-guestagents
colima start --arch x86_64 --vm-type qemu --cpu 4 --memory 8 --disk 40
docker pull --platform linux/amd64 proteowizard/pwiz-skyline-i-agree-to-the-vendor-licenses:latest
```

`colima start` é preciso de novo depois de reiniciar a máquina. A aplicação encontra o daemon pelo contexto docker que o colima escreve, mesmo lançada pelo Finder.

As variáveis de ambiente que o afinam estão em [[environment-variables]]. Sem conversor a corrida
para no primeiro arquivo de fabricante com uma mensagem nomeando as duas opções.

## Formatos legados

A dll de leitura original do MS-DIAL (`RawDataHandler-Vendor-UnSupported.dll`, do pacote NuGet
upstream) pode ser posta em `<app>/plugins/legacy/`, e `.ibf`, `.cdf` e `.imzML` passam a ser lidos
por ela por reflexão. `.ibf` é puramente gerenciado e funciona; `.cdf` também precisa de
`libnetcdf` (`brew install netcdf`); `.abf` precisa de uma biblioteca nativa só para Windows e não
consegue funcionar em macOS ou Linux — converta-o para mzML.

## Plugins

Leitores adicionais implementam `IRawFileReaderPlugin` e são carregados de `<app>/plugins`
(qualquer subpasta) e de `$MILX_PLUGINS`. Números de prioridade mais baixos vencem quando dois
conseguem ler um arquivo. Um plugin Thermo RawFileReader é o candidato óbvio: a biblioteca .NET da
Thermo corre em macOS e Linux mas é licenciada à parte, por isso não vem no pacote.

## O que o cache de survey muda

Nada do que está acima se repete quando um projeto é reaberto: depois da primeira leitura as
varreduras de survey de um arquivo são guardadas em disco, e o Explorer, a grade de picos e os
espectros vêm de lá em milissegundos. O processamento lê sempre o arquivo original. Veja
[[caches-and-storage]].
