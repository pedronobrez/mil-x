---
title: Sobre o OpenDIAL
section: Help
order: 62
summary: Licença, atribuição, citação, e o que o OpenDIAL não é.
---

# Sobre o OpenDIAL

O OpenDIAL é um port aberto, mantido pela comunidade, do motor de processamento do MS-DIAL 5 —
detecção de picos, deconvolução de MS/MS, anotação por biblioteca espectral e alinhamento — para
macOS, Linux e Windows, com uma interface de usuário multiplataforma própria.

## Licença

O OpenDIAL é construído sobre o código-fonte do MS-DIAL publicado sob a GNU Lesser General Public
License v3.0, e é ele próprio distribuído sob a LGPL-3.0. As fontes do MS-DIAL são usadas quase sem
alteração; as seis correções estão listadas em [[architecture#As seis correções upstream]] e
distribuídas como diff.

O leitor nativo de `.wiff` liga-se aos assemblies Clearcore2 da SCIEX, redistribuídos sob a licença
do WIFF Reader Distributable SDK da SCIEX, que é copiada ao lado deles em `plugins/sciex`. Não fazem
parte do repositório do OpenDIAL. A ponte msconvert usa o ProteoWizard, cuja imagem Docker traz as
bibliotecas dos fabricantes sob as suas próprias licenças. A interface usa as fontes Inter e
JetBrains Mono NL sob a SIL Open Font License.

## Atribuição

O OpenDIAL não é afiliado, endossado ou suportado pelo RIKEN, pela UC Davis ou pela equipe de
desenvolvimento do MS-DIAL. Se usar o OpenDIAL numa publicação, cite os artigos originais do
MS-DIAL:

- Tsugawa, H. et al. *MS-DIAL: data-independent MS/MS deconvolution for comprehensive metabolome analysis.* Nature Methods 12, 523–526 (2015).
- Tsugawa, H. et al. *A lipidome atlas in MS-DIAL 4.* Nature Biotechnology 38, 1159–1163 (2020).

O modelo ortogonal segue Trygg, J. e Wold, S., *Orthogonal projections to latent structures
(O-PLS)*, Journal of Chemometrics 16, 119–128 (2002). A correção de deriva segue o procedimento
QC-RLSC de Dunn, W. B. et al., Nature Protocols 6, 1060–1083 (2011).

## O que o OpenDIAL não é

Não é uma reimplementação dos algoritmos do MS-DIAL: os números que uma corrida produz são os do
MS-DIAL, e são validados contra o MS-DIAL no Windows sobre os mesmos dados (veja
[[processing#Números a esperar]]). Também não é um substituto da interface Windows do MS-DIAL:
imagem, navegação de mobilidade iônica, integração com o MS-FINDER e a estatística Notame não
foram portadas. E não é uma ferramenta de quantificação dirigida; esse é o trabalho do OpenQuant,
e [[openquant]] é a ponte.
