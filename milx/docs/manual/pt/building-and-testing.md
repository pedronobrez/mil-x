---
title: Construir e testar
section: Under the hood
order: 52
summary: Construir a aplicação e o console a partir do código, as suítes de testes, as referências de regressão visual, e o smoke test que comanda o pacote instalado.
---

# Construir e testar

## Construir

```bash
bash milx/scripts/setup-macos.sh          # o SDK do .NET 8 em ~/.dotnet, sem sudo
bash milx/scripts/fetch-sciex-assemblies.sh   # opcional: o SDK da SCIEX, para .wiff nativo
bash milx/scripts/build-gui.sh            # -> milx/dist/milx-desktop-<rid>/
bash milx/scripts/make-app-bundle.sh      # -> milx/dist/MIL-X.app
ditto milx/dist/MIL-X.app /Applications/MIL-X.app
bash milx/scripts/build-cli.sh            # -> milx/dist/milx-cli-<rid>/
```

Toda construção passa `-p:UseOpenRawData=true`, o interruptor que substitui o leitor fechado pelo
aberto em toda a árvore upstream. O pacote é autocontido e assinado ad-hoc, que é o que o Apple
Silicon precisa para o correr; não é notarizado. A versão vem de `milx/Directory.Build.props`
e é carimbada na aplicação, no pacote e na página [[versions]] deste manual.

Para desenvolver sem publicar:

```bash
export DOTNET_ROOT=$HOME/.dotnet PATH=$HOME/.dotnet:$PATH
dotnet run --project milx/src/MilX.Desktop -c Release -p:UseOpenRawData=true
```

O Linux constrói da mesma forma com `linux-x64`; o console é coberto por CI no Ubuntu. O Windows
constrói com `win-x64`.

## O SDK da SCIEX

`fetch-sciex-assemblies.sh` copia os assemblies Clearcore2 do pacote Python `alpharaw`, que os
redistribui sob a licença do WIFF Reader Distributable SDK da SCIEX, para `milx/vendor/sciex`
junto com a licença. Não são commitados. Quando a pasta está presente a construção compila o
plugin e espelha-o em `plugins/sciex`; quando não está, o `.wiff` passa pelo msconvert.

## Os testes

```bash
bash milx/scripts/test.sh
```

| Suíte | O que contém |
| --- | --- |
| `MilX.RawData.Tests` | o leitor de mzML: codificações, idas e voltas de Numpress, a semântica de que o código a jusante depende, a ponte para fabricantes |
| `MilX.Pipeline.Tests` | o depósito de curadoria, o editor de picos, a busca na biblioteca, os caches, a estatística (com testes de equivalência entre a validação cruzada simples e a rápida), a corrida sintética ponta a ponta, e testes com dados reais que correm quando `MILX_TEST_ALIGNMENT` aponta para um resultado real |
| `MilX.Desktop.Tests` | as vistas reais, sem tela: a área de revisão, a área de estatística, os gestos, as fontes, a sonda, o manual nos dois idiomas, e os quadros de regressão visual |
| `MilX.Interop.OpenQuant.Tests` | o CSV de componentes e a importação de lote |
| `MilX.Plugins.SciexWiff.Tests` | o leitor nativo, sobre um `.wiff` real quando `MILX_TEST_WIFF` nomeia um |

`test.sh` gera depois um estudo sintético LC-MS/MS DDA e um GC-MS e corre o console sobre cada um
ponta a ponta. As suítes upstream também passam em Apple Silicon com o leitor aberto, exceto cinco
testes que fixam separadores de caminho do Windows ou terminações de linha CRLF.

## Os testes do manual

`ManualTests` seguram este manual à aplicação, na edição em inglês e na em português. Todo wikilink
tem de nomear uma página que existe; toda imagem tem de estar embutida; toda página precisa do seu
front matter; a página [[versions]] tem de nomear a versão da construção; e todo botão, aba, item
de menu e caixa de seleção da interface tem de aparecer em algum lugar do texto do manual, de modo
que um controle acrescentado sem uma frase sobre ele falha a construção. A busca tem de encontrar a
correção de deriva a partir da palavra `drift`.

## Os testes de regressão visual

`VisualRegressionTests` renderizam uma área de trabalho com Skia e comparam o quadro com um guardado
sob `tests/MilX.Desktop.Tests/Baselines/`. Os quadros viajam entre máquinas porque a aplicação
leva as suas duas fontes — Inter e JetBrains Mono NL — e a comparação faz a média de blocos de três
por três antes de comparar, o que esquece o hinting e guarda tudo o que se moveu. No máximo um por
cento dos blocos pode diferir; apagar um décimo da largura falha.

Quando um quadro falha, olhe o arquivo renderizado que a mensagem nomeia ao lado da referência. Se
a interface mudou de propósito:

```bash
MILX_UPDATE_BASELINES=1 dotnet test milx/tests/MilX.Desktop.Tests -c Release -p:UseOpenRawData=true
```

e olhe o que foi escrito antes de o commitar.

## O smoke test

Tudo acima testa o código. O smoke test comanda a coisa que é instalada:

```bash
milx/scripts/smoke-ui.py --project ~/…/Project-2609071200.mdproject
milx/scripts/smoke-ui.py --process ~/…/raw-folder --method method.txt --library lib.msp
```

Lança `/Applications/MIL-X.app` pelo LaunchServices com uma pasta de configurações isolada, e lê
o que a janela diz estar mostrando a partir do arquivo de sonda (veja
[[environment-variables#Comandar a aplicação por script]]) em vez de adivinhar pelos pixels. A
primeira forma abre um projeto processado e verifica o título, a tabela de íons, todo atalho de
área de trabalho nas duas teclas modificadoras, um clique numa aba, e a janela da tabela de íons:
destaca a tabela, arrasta a janela pela barra de título sobre a principal com o mouse pressionado,
vê o lugar de encaixe acender, solta, e vê a tabela reencaixar. Pousa também numa feature cujo
espectro de produto está espelhado contra a biblioteca, confere que o painel diz isso, e grava esse
espelho como figura clara; um projeto sem nenhum MS/MS — processado antes do conserto do precursor
por varredura — faz esse passo se afastar com um aviso em vez de falhar. A segunda escreve um projeto
a partir dos arquivos brutos da pasta, pressiona `⌘R`, espera a corrida, verifica que os `.wiff`
foram lidos pelo plugin dentro do pacote e que as exportações e o projeto foram gravados, e depois
faz o que um revisor faz pelo teclado e pelo mouse: digita um id de feature no filtro, digita uma
janela de integração e clica **Apply to all**, clica **Save review**, verifica o arquivo de
marcações e as cópias de segurança em disco, exporta a tabela revisada e a lista do OpenQuant (o
único passo que passa pelo painel de salvar do sistema é entregue pelo canal de comandos), abre o
manual com `F1` e busca nele. `--keep` deixa a aplicação a correr para olhar; `--shots` guarda as
capturas de tela.

Cinco coisas sobre o macOS tornam isto menos óbvio do que parece: o `click at` do System Events
não faz nada numa janela Avalonia, porque passa pela camada de acessibilidade, de modo que eventos
reais de mouse são enviados com `cliclick`; as teclas têm de ser enviadas como códigos de tecla, já
que um evento de caractere nunca chega a um atalho; a janela tem de estar à frente antes do clique;
uma tecla vai para o que estiver à frente no instante em que é enviada, não para uma aplicação à
escolha, de modo que o script confere que a janela ficou mesmo à frente e pressiona de novo quando
nada aconteceu — uma tecla que caiu noutra aplicação está perdida, e esperar mais não a traz de
volta; e uma dica de ferramenta é uma janela por si, que o sistema lista antes da verdadeira, de
modo que o quadro a partir do qual um clique é calculado é pedido pelo nome da janela em vez de
tomado como "window 1". `open --env` não consegue aplicar um ambiente a uma instância que já corre, de modo que o
script garante que nenhuma corre, e espera os poucos segundos em que o sistema ainda responde a um
lançamento com um seco `-600` pela instância que acabou de guardar. Um pedido de privacidade do macOS — a primeira vez que a
aplicação, ou o processo que a lançou, lê uma pasta protegida — para a corrida até uma pessoa
responder; o script não consegue nem deve. `scripts/ui-drive.sh` faz as mesmas primitivas uma de
cada vez para uma captura de tela ou uma olhada.

As duas formas terminam na área Statistics: leem o conjunto de análise da sonda
(`statistics.source`, `statistics.features`, `statistics.standards`), percorrem as páginas
**Volcano plot**, **Principal components**, **Heatmap** e **Lipid enrichment** pelo comando
`selectStatisticsPage`, capturam cada uma, clicam **Build** no mapa de calor e **Compute** no
enriquecimento e verificam que os dois respondem, e gravam o volcano plot como SVG e PNG por
`exportChart`, que nomeia a página, o índice do gráfico nela, o formato, a escala e o caminho.

## Construir este manual

```bash
python3 milx/scripts/build-manual.py     # -> milx/dist/MIL-X-manual-<version>.pdf e .html
python3 milx/scripts/build-manual.py --lang pt   # -> milx/dist/MIL-X-manual-pt-<version>.pdf e .html
```

As páginas sob `docs/manual` (e `docs/manual/pt` para a edição em português) são embutidas na
aplicação no momento da construção e compiladas num só documento pelo script, com os wikilinks
transformados em links internos; o pandoc renderiza o HTML e o Google Chrome imprime o PDF. O
manual é atualizado com toda versão; a página [[versions]] traz as notas.
