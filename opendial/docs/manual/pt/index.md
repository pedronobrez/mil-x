---
title: Manual do OpenDIAL
section: Start
order: 1
summary: O que é o OpenDIAL, o que este manual cobre, e por onde começar a ler.
---

# Manual do OpenDIAL

O OpenDIAL é um port aberto e multiplataforma do motor de processamento do **MS-DIAL 5** para
metabolômica e lipidômica untargeted — detecção de picos, deconvolução de MS/MS, anotação por
biblioteca espectral e alinhamento — com uma aplicação de desktop própria para macOS, Linux e
Windows. O motor é o do MS-DIAL, usado sem alteração. O que o OpenDIAL acrescenta à volta dele é
uma camada aberta de dados brutos que lê mzML e `.wiff` da SCIEX nativamente e faz a ponte para os
outros formatos de fabricante, uma área de revisão construída em torno do próprio ciclo de
curadoria do MS-DIAL, e uma área de estatística para ver um conjunto de dados por inteiro.

Este manual é a referência completa da aplicação: toda área de trabalho, todo controle, todo
arquivo que lê e grava, toda configuração e variável de ambiente, e o que fazer quando algo dá
errado. É o mesmo texto na janela **Help** da aplicação e no PDF, construído de um só conjunto de
páginas, e é atualizado com toda versão — a página [[versions]] diz o que mudou.

## Os dois idiomas

O manual existe em inglês e em português, página a página. O botão **English** / **Português** no
topo da janela de ajuda troca de um para o outro na mesma página, e a escolha fica guardada para a
próxima vez. Os nomes dos controles — botões, abas, menus — ficam em inglês nos dois, porque é
assim que aparecem na tela. Cada idioma tem o seu PDF.

## Como ler

As páginas ligam-se umas às outras entre colchetes duplos, como num cofre do Obsidian:
[[concepts]] é um link, e clicar nele abre essa página. No fundo de toda página, **Referenced by**
lista as páginas que apontam para ela, de modo que o manual pode ser lido nas duas direções. A
caixa de busca no topo da janela de ajuda encontra páginas por qualquer palavra nelas, o melhor
resultado primeiro, e marca as palavras na página que abre. `⌘F` foca-a; `Esc` limpa-a; `⌘[` e `⌘]`
voltam e avançam.

Pressionar **F1** na aplicação abre o manual na página da área de trabalho que está sendo mostrada.

## Por onde começar

- Novo no OpenDIAL: [[getting-started]], depois [[concepts]].
- Vindo do MS-DIAL no Windows: [[concepts#O que é igual ao MS-DIAL]], depois [[review-tags]] e [[projects-and-files]] — os seus projetos e a sua curadoria vêm junto.
- Montar um lote: [[new-project-wizard]], [[samples-workspace]], [[method-workspace]], [[processing]].
- Revisar um resultado: [[analytics-workspace]] é a visão geral; [[ion-table]], [[review-tags]], [[evidence-panels]], [[reintegration]] e [[annotation]] descem um nível.
- Olhar o conjunto de dados por inteiro: [[statistics-workspace]], depois [[one-factor-analysis]], [[internal-standards]], [[pathways]] e, para um desenho com dois fatores, [[two-factor-analysis]]; os gráficos saem como SVG ou PNG, veja [[chart-export]].
- Tirar os dados: [[exports]] e [[openquant]].
- Algo está errado: [[troubleshooting]].

## As páginas

**Início** — [[getting-started]] · [[concepts]] · [[shell]]

**Áreas de trabalho** — [[new-project-wizard]] · [[samples-workspace]] · [[method-workspace]] · [[processing]] · [[explorer-workspace]] · [[analytics-workspace]] · [[statistics-workspace]] · [[one-factor-analysis]] · [[internal-standards]] · [[pathways]] · [[two-factor-analysis]]

**Revisão** — [[ion-table]] · [[review-tags]] · [[evidence-panels]] · [[reintegration]] · [[annotation]] · [[exports]] · [[chart-export]]

**Dados e arquivos** — [[projects-and-files]] · [[raw-data-formats]] · [[openquant]] · [[caches-and-storage]]

**Referência** — [[settings]] · [[keyboard-shortcuts]] · [[method-parameters]] · [[environment-variables]] · [[command-line]] · [[glossary]]

**Por dentro** — [[algorithms]] · [[architecture]] · [[building-and-testing]] · [[biopan-plan]]

**Ajuda** — [[troubleshooting]] · [[versions]] · [[about]]
