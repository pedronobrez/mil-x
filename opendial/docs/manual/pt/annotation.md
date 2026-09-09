---
title: Anotação, pontuações e a biblioteca
section: Reviewing
order: 24
summary: Como o MS-DIAL dá nome a uma feature, o que significam os níveis e as pontuações, como os nomes de lipídios são construídos a partir dos fragmentos, buscar de novo na biblioteca, escolher um nome à mão, e por que duas corridas podem discordar.
---

# Anotação, pontuações e a biblioteca

## Como uma feature ganha o seu nome

Durante o processamento, o espectro de produto deconvoluído de cada pico é pontuado contra todo
registro da biblioteca dentro da tolerância de MS1 do seu precursor (0,01 Da por padrão). A
pontuação combina o produto escalar ponderado, o produto escalar reverso, a porcentagem de picos
casados, a similaridade de massa e — quando o método assim diz — a similaridade de tempo de
retenção. Um registro passa quando todo corte do método é cumprido: pontuação total (60 % por
padrão), produtos escalares ponderado, simples e reverso (0,4), porcentagem de picos casados (0,2),
mínimo de casamentos de espectro (1). O melhor registro que passa é a anotação; os outros são
guardados como **candidatos**. Com **Only report top hit** desligado, mais são guardados. Todo
limiar está em [[method-parameters#Identification]].

Um pico sem espectro de produto ainda pode ser nomeado só pela massa do precursor, como `no MS2:
name`; um casamento abaixo do corte de pontuação total é guardado como `low score: name`. A tabela
de íons lê esses prefixos de volta como o **nível** — confident, suggested, m/z only — e o filtro
de anotação usa-os. Veja [[concepts#Níveis de anotação]].

Uma feature alinhada toma o seu nome da injeção **representativa**: aquela cujo pico pontuou
melhor. Assim o mesmo composto pode levar um nome ligeiramente diferente em duas corridas quando
uma injeção diferente venceu.

## Nomes de lipídios

O nome que uma feature de lipídio carrega não é o nome do registro da biblioteca. O MS-DIAL
reescreve-o a partir dos fragmentos que observou, de modo que um registro vira `LPC 16:0` ao nível
de espécie, `LPC 16:0/0:0` quando uma cadeia é sustentada, e uma posição sn completa só quando os
fragmentos o dizem. A linha **Lipid evidence** nas pontuações de MS/MS diz que nível foi atingido,
e a tabela de candidatos marca **Class**, **Chains** e **sn**. Vindo do Windows: o OpenDIAL corre o
mesmo código e concorda — de 1 462 features partilhadas entre uma corrida Windows e uma corrida
macOS dos mesmos dados, três levam nome diferente com os dois lados pontuando o mesmo registro, e
essas três viram nas duas direções.

## As bibliotecas

O método nomeia até três:

- uma **biblioteca espectral MSP** — a principal; `.msp`, `.msp2` ou `.lbm2`;
- uma **biblioteca de texto** de tempo de retenção e m/z, para anotação só por massa e tempo;
- uma biblioteca de lipídios **LBM**, quando o arquivo de método nomeia uma.

A MSP é carregada uma vez por corrida e, para a revisão, uma vez por sessão: é de onde o espelho de
MS/MS tira a referência e o que a busca na biblioteca lê. Quando o projeto leva a biblioteca dentro
de si (um `.mdproject` leva), essa cópia é usada; senão o arquivo que o método nomeia é lido, de
modo que o caminho ainda tem de existir. Uma corrida sem biblioteca deixa toda feature
desconhecida.

## Buscar de novo na biblioteca

A corrida guarda só os seus melhores casamentos. Quando o composto certo não está entre eles, a
aba Candidates pergunta de novo à biblioteca por esta feature, com as suas próprias tolerâncias —
**MS1** em Da, **MS2** em Da, opcionalmente **RT** em minutos — e sem corte de pontuação nenhum,
porque um revisor procura o que existe, não o que passa. A busca corre o mesmo anotador que o
pipeline usou, de modo que uma pontuação aqui significa o mesmo que uma lá; sem espectro de
produto todo registro à mesma massa empata, e a massa mais próxima decide. **Back to the run's
matches** volta à lista guardada. Alargar MS1 para 0,05 Da é o primeiro movimento habitual;
alargar MS2 ajuda um espectro ruidoso.

## Escolher um nome à mão

**Use this annotation** toma o candidato selecionado — da lista da corrida ou de uma busca — como o
nome da feature. Substitui o nome na tabela de íons, nas exportações e na estatística, mostra uma
etiqueta **hand-picked**, e é gravado em `<alignment>_curation.json` com **Save review**. A
anotação da própria corrida não se perde: **Back to the automatic name** restaura-a. A tabela
revisada marca essas features como *Manually annotated*.

## Por que duas corridas podem discordar

A detecção de picos, a atribuição de MS/MS e a deconvolução não dependem da biblioteca e concordam
entre plataformas. A anotação depende dela por inteiro. Um projeto no Windows que registrou a sua
biblioteca como `POS_GLDB_260406_2` e uma corrida aqui contra `Pos_GLDB_v0-1-0-alpha.msp` não são
comparáveis pelo nome: duas em cada cinco anotações do Windows estão em massas para as quais a
biblioteca alfa não tem registro a 0,01 Da. `ResultCompare --check-library <project>
<library.msp>` pergunta se uma biblioteca poderia sequer ter produzido as anotações de uma corrida;
veja [[command-line]]. Para comparar anotação, aponte as duas para a mesma construção de
biblioteca.
