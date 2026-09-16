---
title: A janela
section: Start
order: 4
summary: Os menus, as abas das áreas de trabalho, a barra de status, o log, a faixa de progresso e como a janela lida com trabalho não salvo.
---

# A janela

Uma janela guarda uma sessão: um projeto, um lote, um resultado. As áreas de trabalho são abas ao
longo do topo; a barra de menus, o log e a barra de status pertencem à janela e ficam iguais
qualquer que seja a área de trabalho em exibição.

## Abas das áreas de trabalho

**Explorer**, **Analytics**, **Method**, **Samples**, **Statistics**, nessa ordem, selecionadas
com um clique ou com `⌘1` a `⌘5` (`Ctrl+1` a `Ctrl+5` num teclado sem tecla de comando). À direita
das abas aparece o nome do projeto, com uma etiqueta **results loaded** quando há um resultado.
Cada área de trabalho tem a sua própria página: [[explorer-workspace]], [[analytics-workspace]],
[[method-workspace]], [[samples-workspace]], [[statistics-workspace]].

Abrir um projeto processado pousa em Analytics; um não processado, em Samples; um arquivo bruto,
no Explorer; iniciar uma corrida muda para Analytics.

## O título e o marcador de alterações

O título diz `MIL-X — <nome do projeto>`, com um `•` depois enquanto o projeto tem alterações
não salvas: um lote editado, um método editado, uma corrida cujo projeto não foi gravado. Fechar
a janela, abrir outro projeto ou começar um novo enquanto o marcador está presente pergunta **Save
this project first?** com **Cancel**, **Discard** e **Save**.

## Menus

### File

| Item | Atalho | O que faz |
| --- | --- | --- |
| New project… | `⌘N` | o [[new-project-wizard]] |
| Open project… | `⌘O` | um `.milx` ou um `.mdproject` |
| Open results folder… | | uma pasta escrita por uma corrida, pelo console ou pelo MS-DIAL, sem arquivo de projeto; o lote é reconstruído a partir dos arquivos de resultado |
| Recent projects | | os últimos dez, mais recente primeiro; um caminho que já não existe é retirado da lista quando escolhido |
| Save project | `⌘S` | grava o `.milx`; pede um local na primeira vez |
| Save project as… | | grava-o noutro lugar; a pasta de resultados passa a ser `results` ao lado |
| Add data files… | | arquivos brutos para o lote, uma linha por injeção |
| Add folder… | | todo arquivo bruto suportado diretamente dentro de uma pasta; as pastas `.d` da Agilent e da Bruker só podem ser adicionadas assim, porque o Finder não consegue entregar uma pasta a uma aplicação |
| Import MIL-Q batch (.oqproj)… | | as amostras de um projeto OpenQuant com os seus tipos, grupos, diluições e comentários; veja [[openquant]] |
| Close project | | esvazia a sessão |
| Settings… | | tema, cache, msconvert; veja [[settings]] |
| Quit | `⌘Q` | |

### View

| Item | Atalho | O que faz |
| --- | --- | --- |
| Log | `⌘L` | mostra ou esconde o painel de log sob a área de trabalho |
| Progress band | | esconde a faixa quando a corrida acabou |
| Explorer: uncheck all channels | | desmarca todos os canais marcados na árvore do Explorer |
| Explorer: show TIC of every sample | | expande todos os arquivos e marca o seu TIC |
| Explorer: collapse tree | | |

### Process

| Item | Atalho | O que faz |
| --- | --- | --- |
| Process batch | `⌘R` | corre o pipeline sobre o lote; veja [[processing]] |
| Cancel run | | para-o no próximo ponto de verificação |
| Re-export alignment matrix… | | uma matriz de alturas e áreas de toda feature contra toda injeção, em TSV; veja [[exports]] |
| Export to MIL-Q… | | as features listadas como uma lista de componentes do OpenQuant; veja [[openquant]] |
| Open output folder | | a pasta de resultados no Finder |
| Choose output folder… | | onde a próxima corrida grava |
| Load method file… | | substitui o método pelo conteúdo de um arquivo; veja [[method-workspace]] |
| Save method file… | | grava o método atual como um arquivo compatível com o console |

### Workspace

As cinco áreas de trabalho com os seus atalhos.

### Help

| Item | Atalho | O que faz |
| --- | --- | --- |
| MIL-X manual | `F1` | este manual, na primeira página |
| Help for this workspace | `⌘⇧?` | a página da área de trabalho em exibição |
| Keyboard shortcuts | | [[keyboard-shortcuts]] |
| Troubleshooting | | [[troubleshooting]] |
| About MIL-X | | a versão, a versão do MS-DIAL que acompanha, o runtime, a licença |

`F1` também abre a página da área de trabalho atual quando o manual já está aberto.

## A faixa de progresso

Enquanto uma corrida decorre aparece uma faixa sob as abas: a etapa (Setup, Converting,
Processing, Export, Alignment, Project, Done), o arquivo em que se trabalha, a última mensagem, a
porcentagem global, um botão **Cancel**. Nada é modal: as áreas de trabalho continuam utilizáveis
enquanto o motor trabalha. A faixa fica depois da corrida para que o resumo possa ser lido; o `✕`
ou **View ▸ Progress band** esconde-a.

## O log

`⌘L` abre um painel sob a área de trabalho com cada linha que o motor imprimiu, com hora, em fonte
monoespaçada: que biblioteca carregou e quantos registros, que arquivos foram lidos nativamente e
quais foram convertidos, o progresso por arquivo, a contagem do alinhamento, os arquivos
exportados e qualquer erro por inteiro. Guarda as últimas cinco mil linhas. Quando uma corrida
falha o log abre sozinho. As linhas podem ser selecionadas e copiadas.

## A barra de status

O lado esquerdo é a última coisa que a aplicação fez ou está fazendo — `Opened 8 file(s) from …`,
`Processing…`, `Review saved to …`, `Export failed: …`. O lado direito é a pasta de resultados. A
maioria das áreas de trabalho também tem uma linha de status própria sob o seu conteúdo: a área
Samples conta as suas injeções por tipo, a área Method resume o método.

## Documentos a partir do Finder

O bundle registra `.milx` (e `.odproj`, o seu nome até a 1.0), `.mdproject`, `.oqproj`/`.opvproj` e os formatos brutos (`.mzML`,
`.wiff`, `.wiff2`, `.raw`, `.abf`, `.ibf`, `.cdf`, `.lcd`, `.qgd`). Abrir um deles a partir do
Finder, num arranque frio ou com a aplicação em execução, faz o que a extensão diz: um projeto abre
como projeto, um lote OpenQuant é importado, um arquivo bruto vai para o Explorer.

## Temas

Claro, escuro, ou seguindo o sistema, a partir de [[settings]]. Toda a interface — gráficos
incluídos — segue a escolha.
