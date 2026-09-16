---
title: Sobre o MIL-X
section: Help
order: 62
summary: Licença, atribuição, citação, e o que o MIL-X não é.
---

# Sobre o MIL-X

O MIL-X é um port aberto, mantido pela comunidade, do motor de processamento do MS-DIAL 5 —
detecção de picos, deconvolução de MS/MS, anotação por biblioteca espectral e alinhamento — para
macOS, Linux e Windows, com uma interface de usuário multiplataforma própria.

## O nome

**MIL** é *Multi-omics Identification Laboratory*; **X** é exploração, a metade não dirigida do
trabalho, em que um lote entra, alguns milhares de features saem e o serviço é decidir o que elas
são. **MIL-Q** é quantificação, a metade dirigida — o OpenQuant de hoje, renomeado quando a sua
própria 1.0 chegar. *Explore, depois quantifique* é o que os dois fazem juntos, e **Export to
OpenQuant…** é a passagem.

Até a 1.0 este programa era o **OpenDIAL**. O nome antigo continua entendido onde quer que tenha
sido anotado: projetos `.odproj` abrem, variáveis `OPENDIAL_*` funcionam, a pasta de configurações
antiga é lida uma vez. [[versions#1.0.0 — setembro de 2026]] lista cada um deles.

A marca é uma cabeça de vaca, de frente. MIL-X se diz como *milks*; o ícone do próprio MS-DIAL é
um telefone de disco, então o trocadilho é uma homenagem, não uma piada às custas do projeto-pai. O
nome é composto numa fonte monoespaçada porque é uma designação, não uma palavra.

## Licença

O MIL-X é construído sobre o código-fonte do MS-DIAL publicado sob a GNU Lesser General Public
License v3.0, e é ele próprio distribuído sob a LGPL-3.0. As fontes do MS-DIAL são usadas quase sem
alteração; as seis correções estão listadas em [[architecture#As seis correções upstream]] e
distribuídas como diff.

O leitor nativo de `.wiff` liga-se aos assemblies Clearcore2 da SCIEX, redistribuídos sob a licença
do WIFF Reader Distributable SDK da SCIEX, que é copiada ao lado deles em `plugins/sciex`. Não fazem
parte do repositório do MIL-X. A ponte msconvert usa o ProteoWizard, cuja imagem Docker traz as
bibliotecas dos fabricantes sob as suas próprias licenças. A interface usa as fontes Inter e
JetBrains Mono NL sob a SIL Open Font License.

## Atribuição

O MIL-X não é afiliado, endossado ou suportado pelo RIKEN, pela UC Davis ou pela equipe de
desenvolvimento do MS-DIAL. Se usar o MIL-X numa publicação, cite os artigos originais do
MS-DIAL:

- Tsugawa, H. et al. *MS-DIAL: data-independent MS/MS deconvolution for comprehensive metabolome analysis.* Nature Methods 12, 523–526 (2015).
- Tsugawa, H. et al. *A lipidome atlas in MS-DIAL 4.* Nature Biotechnology 38, 1159–1163 (2020).

O modelo ortogonal segue Trygg, J. e Wold, S., *Orthogonal projections to latent structures
(O-PLS)*, Journal of Chemometrics 16, 119–128 (2002). A correção de deriva segue o procedimento
QC-RLSC de Dunn, W. B. et al., Nature Protocols 6, 1060–1083 (2011).

## O que o MIL-X não é

Não é uma reimplementação dos algoritmos do MS-DIAL: os números que uma corrida produz são os do
MS-DIAL, e são validados contra o MS-DIAL no Windows sobre os mesmos dados (veja
[[processing#Números a esperar]]). Também não é um substituto da interface Windows do MS-DIAL:
imagem, navegação de mobilidade iônica, integração com o MS-FINDER e a estatística Notame não
foram portadas. E não é uma ferramenta de quantificação dirigida; esse é o trabalho do OpenQuant,
e [[openquant]] é a ponte.
