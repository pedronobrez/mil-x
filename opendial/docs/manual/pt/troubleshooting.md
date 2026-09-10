---
title: Solução de problemas
section: Help
order: 60
summary: Sintomas, causas e correções — a aplicação não abre, um arquivo não lê, uma corrida não encontra nada, um atalho não faz nada, um resultado não pode ser editado, um modelo parece bom demais.
---

# Solução de problemas

Cada entrada é um sintoma, o que está por trás, e o que fazer. O log (`⌘L`) é o primeiro lugar a
olhar para tudo o que uma corrida fez; a barra de status para tudo o que a janela fez.

## Abrir a aplicação

**O macOS diz que a aplicação está danificada, ou não pode ser aberta.** O pacote é assinado ad-hoc
e não notarizado, e uma cópia que chegou por download ou AirDrop está em quarentena. Clique com o
botão direito ▸ **Open** ▸ **Open** uma vez. Um pacote construído na mesma máquina não tem a
bandeira de quarentena e abre de imediato. Se uma cópia foi instalada arrastando no Finder em vez
de `ditto`, a assinatura pode ter-se perdido; reinstale com `ditto`.

**O Finder abre projetos na cópia errada da aplicação.** Dois pacotes reclamam os mesmos tipos de
documento. Mantenha um em `/Applications`; o script de empacotamento desregistra a cópia de `dist/`
enquanto existe uma instalada.

**Um pedido de privacidade aparece e a aplicação parece congelada.** O macOS pergunta uma vez, por
aplicação, antes de ler uma pasta protegida (Documents, Desktop, dados de outras aplicações). Uma
leitura de arquivo bloqueia até o pedido ser respondido. Responda. Quando a aplicação foi lançada
por um script, o pedido nomeia o processo que a lançou.

## Ler arquivos

**O `.wiff` mostra `wiff · msconvert` em vez de `wiff · native`.** O plugin da SCIEX não está no
pacote (`Contents/MacOS/plugins/sciex`): a construção foi feita sem `vendor/sciex`. Corra
`fetch-sciex-assemblies.sh` e reconstrua, ou deixe o msconvert converter o arquivo.

**`No spectra could be read from …`, ou `Could not read file`.** Para um `.wiff`, o `.wiff.scan`
tem de estar ao lado. Para um arquivo de fabricante sem leitor nativo, o msconvert é necessário:
veja a entrada seguinte. Para um mzML, o arquivo pode estar truncado; o log mostra até onde o
leitor chegou.

**`Cannot read … on this platform: vendor formats need ProteoWizard msconvert`.** Nenhum conversor
foi encontrado. Instale o Docker Desktop e puxe a imagem do ProteoWizard, ou aponte [[settings]]
para um msconvert nativo, ou converta o arquivo noutro lugar e ponha o `.mzML` ao lado da origem.
Veja [[raw-data-formats#A ponte msconvert]].

**A conversão demora minutos por arquivo.** Sob Docker em Apple Silicon a imagem corre sob emulação
x86-64; esse é o custo. Acontece uma vez por arquivo.

**O log diz `rosetta error: invalid gdt selector index` e o msconvert sai com código 1.** O
contentor está correndo sob Rosetta, que não consegue correr o Wine da imagem. Dê ao Docker uma
máquina virtual QEMU: com o colima, `colima delete` e `colima start --arch x86_64 --vm-type qemu`;
no Docker Desktop, desligue "Use Rosetta for x86_64/amd64 emulation". Veja
[[raw-data-formats#A ponte msconvert]].

**O colima recusa arrancar com `guest agent binary could not be found for Linux-x86_64`.** A VM
x86-64 precisa dos agentes extra do lima: `brew install lima-additional-guestagents`.

**`Raw data file not found (was the project folder moved?)`.** O projeto grava os caminhos dos
arquivos brutos; relativos quando estão perto, absolutos senão. Mova os arquivos brutos com o
projeto, ou adicione-os de novo na área Samples.

**O `.abf` não abre.** Não consegue no macOS nem no Linux: o leitor da Reifycs é uma biblioteca
nativa só para Windows. Converta para mzML.

## Processamento

**A corrida encontra zero picos, ou tempos de retenção absurdos, numa máquina com vírgula
decimal.** Este é o defeito que o port corrige: o MS-DIAL lê números com a localização da máquina.
A aplicação de desktop e o lançador `opendial-cli` forçam a cultura invariante, de modo que não
pode acontecer lá; correr `MSDIALCUI` diretamente pode. Use o lançador, ou defina
`DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1`.

**`Library not found`.** O método nomeia um arquivo MSP que não está nesse caminho. Corrija na área
Method; a última biblioteca usada é oferecida no diálogo de arquivo.

**Toda feature é Unknown.** Nenhuma biblioteca foi dada, ou a polaridade da biblioteca não casa com
a corrida, ou a tolerância de MS1 é apertada demais para o instrumento. O log diz quantos registros
carregaram; zero significa que o arquivo estava vazio ou ilegível.

**Muito menos picos do que o MS-DIAL no Windows encontrou, em dados `.wiff`.** Confira que o plugin
está centroidando com o detector de picos da SCIEX (`OPENDIAL_WIFF_CENTROID` indefinida ou
`sciex`). O método do máximo local reporta alturas cerca de um quarto mais baixas, e o corte de
altura mínima descarta então picos que a corrida Windows guardou. Veja
[[raw-data-formats#SCIEX .wiff]].

**Menos atribuições de MS/MS do que o projeto Windows.** Confira o tipo de aquisição na área
Samples. O MS-DIAL 5.5 marcou os arquivos IDA do conjunto de validação como SWATH, que atribui
espectros por janela de isolamento e é mais folgado do que DDA. Veja
[[processing#Tipos de aquisição]].

**Nomes diferentes do projeto Windows para os mesmos picos.** Quase sempre a biblioteca, não o
processamento: uma construção diferente da biblioteca tem registros em massas diferentes. Aponte
as duas para o mesmo arquivo. `ResultCompare --check-library` diz se uma biblioteca poderia ter
produzido as anotações de uma corrida. Veja [[annotation#Por que duas corridas podem discordar]].

**`Run failed: …` com `AccessViolationException` ao abrir um projeto.** Corrigido na correção
upstream desta versão a `LargeListMessagePack`; uma construção feita antes dela falhava em projetos
com biblioteca grande. Reconstrua.

**Sem memória num estudo muito grande.** O alinhamento corre em memória, como no upstream. Reduza o
lote, ou defina `Alignment light mode: True` no texto do método.

## A janela

**`⌘5` (ou qualquer atalho de área de trabalho) não faz nada.** Corrigido nesta versão; em
construções anteriores os atalhos estavam escritos numa forma que o Avalonia lia como a tecla
errada. Se voltar a acontecer, os `GestureTests` dizem que binding está errado.

**`Ctrl+1` marca uma feature em vez de mudar de área de trabalho.** Por desenho: `Ctrl` com um
dígito é uma marcação de revisão, `⌘` com um dígito é uma área de trabalho. No Linux e no Windows
as áreas de trabalho também estão em `Ctrl`, e as teclas de marcação continuam a funcionar na área
de revisão.

**A faixa de filtros cobre as contagens numa janela estreita.** Corrigido: a faixa rola para o
lado. Alargue a janela se for mais estreita do que 1100 pontos.

**A tabela de íons desapareceu.** Foi destacada numa janela própria, que pode estar atrás da
principal ou noutra tela. **Dock table** traz de volta; `IonTableDetached` em `settings.json`
lembra o estado.

**Um gráfico está ampliado para o nada.** Dê duplo clique, ou **Overview** no Explorer.

**A busca do manual não encontra nada.** Toda palavra tem de casar; tente menos palavras. A busca é
sobre o texto e os títulos das páginas, não sobre os rótulos da interface.

**O manual abre em inglês.** O botão **Português** / **English** no topo da janela de ajuda troca o
idioma, e a escolha fica guardada em `settings.json` para a próxima vez.

## Revisão

**A reintegração ou Split isomer diz que o resultado não pode ser editado.** O resultado foi aberto
de uma exportação `.mdalign` ou de uma pasta sem os seus arquivos `.arf2`; não há contentor para
gravar. Abra o `.mdproject` ou a pasta que tem os arquivos binários.

**`Set a retention window first`.** As caixas from e to estão vazias ou invertidas. `⇧`-arraste um
painel ou digite dois tempos crescentes.

**Save review diz que a revisão não pôde ser salva.** A pasta de resultados é só de leitura, ou um
arquivo está bloqueado por outro processo (o MS-DIAL num disco partilhado). As marcações ficam na
sessão até conseguir gravar.

**Uma edição precisa ser desfeita.** Renomeie os arquivos `.before-curation` de volta sobre os
originais; guardam o alinhamento como a corrida o produziu. Veja
[[projects-and-files#Cópias de segurança]].

**As marcações feitas no MS-DIAL não aparecem.** São lidas de `<alignment>_tags.xml` ao lado do
arquivo de alinhamento; o MS-DIAL grava-o quando a sua própria revisão é salva. O arquivo tem de
estar ao lado do `.arf2` que o OpenDIAL abriu.

## Estatística

**O modelo discriminante separa as classes perfeitamente e o veredito ainda diz que não sobrevive
à validação cruzada.** Isso é o veredito funcionando. Com poucas injeções e milhares de features
um ajuste separa qualquer coisa; só Q² e o p de permutação dizem se aguentaria em dados novos. Leia
o gráfico como um retrato deste lote.

**O modelo ortogonal recusa: `separates two classes, and this batch has 4`.** OPLS-DA é de duas
classes por construção. Use o modelo discriminante, ou defina as classes de modo que só duas
restem (as injeções das outras ainda participam nos componentes principais).

**A correção de deriva não corrige nada.** Menos de três injeções estão com tipo `QC` na área
Samples, ou as suas alturas são zero. A mensagem diz quantas encontrou. A ordem e o lote também
importam: uma ordem de injeção 0 em todo lado é lida como a ordem dos arquivos.

**A rede está vazia.** Nenhuma feature do filtro traz espectro de produto, ou o corte é alto
demais. Baixe-o, ou desmarque **Annotated only**.

## Obter mais detalhe

`OPENDIAL_TRACE=1` imprime no console os avisos de binding do Avalonia. O log guarda as mensagens
do próprio motor. `RawDump` e `WiffProbe` (veja [[command-line]]) mostram o que a camada de dados
brutos lê de um arquivo, independentemente da aplicação.
