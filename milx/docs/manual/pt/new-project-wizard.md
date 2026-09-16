---
title: O assistente de novo projeto
section: Workspaces
order: 10
summary: Os quatro passos que criam um projeto — nome e pasta, amostras, método, resumo — e o que cada um grava.
---

# O assistente de novo projeto

**File ▸ New project…** (`⌘N`) abre um assistente de quatro passos. Ele grava o arquivo de projeto
quando termina, não antes, e toda alteração posterior é salva de volta nesse arquivo. Os passos
são **Project**, **Samples**, **Method** e **Summary**; **Back** e **Next** movem entre eles,
**Cancel** não deixa nada para trás, e o botão do último passo diz **Create project**.

## Passo 1 — Project

Um **Name** e uma **Folder**. O projeto é criado como `<Folder>/<Name>/<Name>.milx`, com uma
pasta `results` ao lado para tudo o que a corrida grava; caracteres que um nome de arquivo não
aceita são substituídos por sublinhados. **Browse…** escolhe a pasta; a pré-visualização por baixo
mostra o caminho exato. Os arquivos brutos ficam onde estão — o projeto registra os seus
caminhos, relativos quando estão perto.

Next fica disponível quando há um nome e a pasta existe.

## Passo 2 — Samples

**Add data files…** escolhe arquivos brutos; **Add folder…** pega todo arquivo bruto suportado
diretamente dentro de uma pasta. Um lote `.wiff` que guarda várias amostras vira uma linha por
amostra, com o nome da amostra dentro do lote. **Remove** retira a linha selecionada. A tabela
mostra o **File**, um nome de **Sample** editável, o **Type** (Sample, Blank, QC, Standard) e a
**Class**, e uma etiqueta **Format** — `mzML`, `wiff · native`, `raw · msconvert` — que diz como o
arquivo será lido; um arquivo que precisa do msconvert é contado na linha de resumo, para que se
saiba antes da corrida. Tudo aqui pode ser alterado depois na [[samples-workspace]].

Next precisa de pelo menos um arquivo.

## Passo 3 — Method

De onde vem o método de processamento:

- **Defaults for LC-MS (DDA, positive, centroid)** — os padrões de LC-MS do MS-DIAL; veja [[method-parameters]].
- **Defaults for GC-MS (EI, RT alignment)** — o ramo GC-MS com alinhamento por tempo de retenção.
- **From a method file** — um `.txt` compatível com o console, por exemplo um salvo de um projeto anterior ou produzido a partir de uma exportação de parâmetros do MS-DIAL por `tools/msdial_param_to_method.py`.

**Library** é a biblioteca espectral MSP que a anotação pesquisa; é opcional, e sem ela toda
feature fica desconhecida. O caminho da biblioteca é lembrado para o próximo projeto. O método
pode ser editado depois na [[method-workspace]].

## Passo 4 — Summary

O caminho, a contagem de amostras e de classes, a origem do método e a biblioteca, num só bloco.
**Process the batch right away** (marcado por padrão) inicia a corrida assim que o projeto é
gravado; senão o projeto abre na área Samples e `⌘R` inicia-a mais tarde. Veja [[processing]].

## O que grava

O arquivo `.milx` descrito em [[projects-and-files]]: o nome, o modo (LC-MS ou GC-MS), a pasta
de resultados, o texto do método, e as amostras com os seus caminhos, nomes, tipos, classes,
tipos de aquisição e ordem. O assistente também lembra a sua pasta, o último arquivo de método e
a última biblioteca nas configurações da aplicação, para que o próximo projeto comece onde este
começou.
