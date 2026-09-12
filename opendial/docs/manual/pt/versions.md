---
title: Versões
section: Help
order: 61
summary: O que cada versão do OpenDIAL trouxe; as notas são atualizadas com toda versão.
---

# Versões

A versão mostrada em **Help ▸ About OpenDIAL** e na sonda da janela é a carimbada em
`opendial/Directory.Build.props`. Esta página nomeia-a, e um teste falha a construção quando não
nomeia.

## 0.5.0 — setembro de 2026

A versão atual. Segue o MS-DIAL 5.5.260817.

**Novo**

- A página **Two factors** da área Statistics: a análise de variância de dois fatores por feature com a interação (somas de quadrados do tipo II, de modo que desenhos desbalanceados e células vazias são tratados), e ASCA sobre a matriz inteira com um teste de permutação por efeito e um gráfico de escores por efeito. O segundo fator é uma coluna **Factor** na área Samples, salva com o projeto. Veja [[two-factor-analysis]].
- O manual em português, ao lado do inglês: **Português** na janela de ajuda, e um segundo PDF.

**Alterado**

- A área Analytics abre no que confirmar um analito exige: a tabela de íons, o pico em toda amostra e o espectro de produto, tudo na tela de uma vez. A grade de picos é um painel próprio acima das abas de evidência em vez da primeira aba, e MS/MS é a aba mostrada por padrão; um divisor entre os dois define as alturas. O tamanho da grade é um único controle **Grid** `3 × 2`.
- A janela da tabela de íons reencaixa ao ser arrastada sobre a parte esquerda da janela principal: o lugar que vai ocupar acende quando a barra de título o cruza, e soltar aí encaixa-a. Enquanto a tabela está fora, os picos e o espectro ocupam toda a largura; a coluna dela não fica mais vazia.

- As teclas de revisão são `⌘⇧` (`Ctrl+Shift`) com um dígito para uma marcação, com C e X para os vereditos, com N para a próxima feature não revisada e com as setas para a seguinte e a anterior. Estão ligadas nas janelas, de modo que funcionam seja qual for o foco, na janela principal enquanto Analytics está na tela e na janela própria da tabela de íons.

**Corrigido**

- Uma sessão que terminava com a tabela de íons na sua própria janela caía no arranque seguinte: a janela era pedida antes de a principal estar na tela. Agora abre depois de a principal ter aberto.
- As teclas de marcação nunca disparavam: estavam escritas como `Ctrl` com um dígito puro, que o Avalonia lê como outra tecla; e `Alt+C` só disparava com o foco dentro da revisão.
- Os rótulos *measured* e *reference* do espelho de espectros desenhavam por cima do m/z de um pico-base na borda direita; cada um toma agora o lado mais vazio da sua metade.
- Depois de salvar, a mensagem da barra de ferramentas de Analytics desenhava por cima dos próprios botões; agora ocupa o espaço que os botões deixam e recorta, com a frase inteira na dica.
- A ponte msconvert está verificada de ponta a ponta em Apple Silicon: o `small.RAW` do ProteoWizard converte pela máquina QEMU x86-64 do colima, pela linha de comando e pela aplicação instalada, em cerca de 30 segundos. A Rosetta não consegue correr o Wine da imagem, e o manual e a mensagem do conversor agora dizem isso e dão o caminho do colima.
- O smoke test clicava no lugar errado sempre que o ponteiro repousava sobre algo com uma dica de ferramenta: a dica é uma janela por si, e o sistema lista-a antes da verdadeira, de modo que o quadro a partir do qual o clique era calculado era o da dica. O quadro é agora pedido pelo nome da janela, e todo clique diz em que ponto da tela foi dado.
- O smoke test falhava de vez em quando num atalho que na verdade nunca fora entregue: uma tecla vai para o que estiver à frente no momento em que é enviada, e a frente pode ser tomada no instante entre pedi-la e a tecla descer. Agora ele confere que a janela ficou à frente, pressiona de novo quando nada aconteceu, diz que aplicação estava segurando a frente quando desiste, e espera os segundos em que o sistema recusa lançar o pacote que acabou de guardar.
- Os trechos de código, as marcas da busca, os cabeçalhos de tabela e as réguas do manual tomavam as cores do tema claro fosse qual fosse o tema da janela, de modo que uma janela escura desenhava o código branco sobre branco. Todo pincel de uma página agora segue a janela em que está, e muda com o tema.

## 0.4.2 — setembro de 2026

Segue o MS-DIAL 5.5.260817.

**Novo**

- As aquisições `.wiff2` da SCIEX são lidas nativamente, pelo `.wiff` que o SCIEX OS grava ao lado de cada uma: os mesmos espectros e nomes de amostra, verificados no lote do ZenoTOF. Um `.wiff2` sozinho ainda passa pelo msconvert, e diz isso.
- Uma injeção por aquisição: adicionar uma pasta com um par `.wiff`/`.wiff2` toma o `.wiff`, e um `.wiff2` escolhido ao lado do seu `.wiff` é trocado por ele com uma nota, de modo que nada é processado duas vezes.
- Um plugin de arquivos brutos que reclama um arquivo e não consegue abri-lo entrega o arquivo à ponte msconvert em vez de falhar a corrida.

## 0.4.1 — setembro de 2026

Segue o MS-DIAL 5.5.260817.

**Alterado**

- A tabela de vias é agora a do próprio BioPAN, transcrita da base arquivada da ferramenta: 97 reações (51 entre classes, 13 sobre os lipídios éter, 3 sobre as bases de esfinganina, 30 entre ácidos graxos) com as suas 249 ligações reação–gene; os doze passos que o OpenDIAL acrescenta estão marcados e ligados por **Beyond BioPAN**, desligados por padrão. As classes éter separam-se em `O-` e `P-`, as ceramidas e esfingomielinas nas suas formas de esfinganina, como o BioPAN as tem.
- Os pesos das reações são comparados como o BioPAN os compara — as próprias razões, não os seus logs — e os limiares são os do BioPAN, unilaterais: 1,282, 1,645 (padrão), 2,054, 2,326.
- Todo gene na tabela é o símbolo humano; os `Scd1` e `Scd3` de camundongo do BioPAN são `SCD` e `SCD5`.
- **Paired** na página Pathways: a comparação pareada do BioPAN, as injeções das duas classes tomadas uma a uma em ordem.
- Uma nova marca para o OpenDIAL: o mesmo quadrado arredondado, gradiente e pico branco, com o cromatograma inteiro atrás do pico em vez de um vizinho coeluído — a corrida untargeted, tudo o que contém. A marca de dois picos, a deconvolução de um analito, vai para o OpenQuant. `tools/make_icon.py --export` grava qualquer das duas como PNGs, um `.iconset`, um `.icns`, um `.ico` e um SVG.
- A legenda de um gráfico vai para o canto com menos coisa por baixo, de modo que o canto nomeado do volcano plot continua legível; o nome do valor do mapa de calor fica sob a barra de cores em vez de atravessar os seus números.
- O nível de espécie segue a regra do BioPAN para as reações de cadeia: um passo que liberta uma cadeia só é desenhado quando esse ácido graxo é medido, um que adiciona uma cadeia só quando o seu acil-CoA é. O nível de ácido graxo são os ácidos graxos livres medidos, como o do BioPAN, não as cadeias dos outros lipídios.
- As notas de projeto dizem onde os dois concordam (a pontuação da via é o mesmo número) e onde não (a regra de cadeia ao nível de espécie, as extensões).

## 0.4.0 — setembro de 2026

Segue o MS-DIAL 5.5.260817.

**Novo**

- A página **Pathways** da área Statistics: o BioPAN do LIPID MAPS sobre os analitos revisados — a rede de reações de lipídios de mamíferos (sessenta e tantas reações sobre umas trinta classes, com os seus genes) ponderada por produto sobre reagente em toda injeção, comparada entre as duas classes da comparação, pontuada como Z, e encadeada em vias; aos níveis de classe, espécie molecular e ácido graxo; desenhada como rede com as injeções das reações ao lado, exportada como tabelas e como SVG ou PNG. Veja [[pathways]] e [[biopan-plan]].
- O smoke test constrói o mapa de calor, calcula o enriquecimento e pontua as vias clicando nos seus botões, e a sonda relata o que cada um respondeu.

**Corrigido**

- A sonda não reescrevia o seu estado quando a análise de um fator mudava, de modo que um mapa de calor podia estar na tela enquanto a sonda dizia que não estava construído.
- O smoke test filtrava a tabela de íons pelo id de uma feature, o que para a feature 0 casava metade da tabela; filtra pelo m/z como impresso.

## 0.3.0 — setembro de 2026

Segue o MS-DIAL 5.5.260817.

**Novo**

- A área Statistics reconstruída em torno do módulo de um fator do MetaboAnalyst, com as suas páginas à esquerda: **Data processing** (valores ausentes, os filtros de variância e de RSD de QC, normalização por amostra, transformação, escalonamento), a **Normalisation check**, **Fold change**, o **Statistical test** (Welch, Student, pareado, Mann–Whitney, Wilcoxon; FDR, Holm, Bonferroni), o **Volcano plot**, **ANOVA** e Kruskal–Wallis com post-hoc LSD de Fisher, **Correlations**, **Pattern search**, o **Random forest**, o **Heatmap** agrupado, **K-means**, e o **Lipid enrichment** (sobre-representação sobre classes, comprimentos de cadeia e insaturação, as mudanças por classe e o mapa de cadeias). Veja [[one-factor-analysis]].
- A fonte de **abundância relativa**: analitos confirmados como razões de área ao padrão interno da sua classe, escolhido por classe no diálogo **Internal standards…**, sugerido a partir de nomes marcados e de cadeia ímpar, e guardado no arquivo lateral de curadoria. Veja [[internal-standards]].
- Todo gráfico numa moldura com opções — título, rótulos, tamanho de ponto, escala de fonte, paleta, escala de cores, grade, legenda, rótulos, elipses, árvores, valores — e **Export…** como SVG (linhas e texto reais) ou PNG a 2×, 4× e 6×. Veja [[chart-export]].
- A página de componentes principais ganhou o scree plot, as elipses de confiança de 95 %, a escolha de componentes e os loadings principais nomeados; a página discriminante um gráfico de VIP com as médias das classes ao lado de cada barra; o dendrograma as suas escolhas de distância e ligação.
- **Reload from the review** quando a revisão muda depois de a análise ter sido carregada; **Drift corrected** agora alimenta os valores corrigidos antes do pré-processamento, de modo que as razões e os testes também os leem.
- O motor: `Distributions` (gama, beta, normal, Student, Fisher, qui-quadrado, hipergeométrica), `Preprocessing`, `Univariate`, `Correlations`, `Clustering` (quatro distâncias, quatro ligações, k-means++), `RandomForest`, `Enrichment`, `LipidNames`, `RelativeAbundance` e `AnalysisTable`, cada um com testes contra valores calculados à mão e contra o lote de fígado revisado.
- O plano para um módulo de vias ao estilo do BioPAN, [[biopan-plan]].

**Alterado**

- A faixa de opções da área Statistics é agora a faixa **Source**; a transformação e o escalonamento passaram para a página **Data processing**, onde está o resto do pré-processamento.
- O manual ganhou quatro páginas e onze figuras; os testes de regressão visual cobrem as páginas novas e o diálogo de padrões.

## 0.2.0 — setembro de 2026

Segue o MS-DIAL 5.5.260817.

**Novo**

- Este manual: na aplicação sob **Help**, com busca e referências cruzadas, e como PDF construído das mesmas páginas.
- A aba **Orthogonal** da área Statistics: OPLS-DA com o S-plot, submetido à mesma validação cruzada e teste de permutação que o modelo discriminante.
- A aba **Drift correction**: QC-RLSC contra os controles de qualidade, lote a lote, com um nivelamento de recurso para lotes com menos de três controles, e um interruptor **Drift corrected** que alimenta os valores corrigidos a toda outra vista.
- A aba **Discriminant**: PLS-DA com VIP, Q² leave-one-out e um teste de permutação, com a validação cruzada feita no espaço que as injeções geram de modo que duas mil features permutam em menos de meio segundo.
- O cache de varreduras de survey, que torna imediato reabrir um projeto, e um cache de espectros de produto ao lado.
- A tabela de íons pode ser destacada numa janela própria, as suas colunas reordenadas, e as duas coisas lembradas.
- O smoke test que comanda o pacote instalado, em duas formas: abrir um projeto, e processar um lote passando por reintegração, salvamento e exportação.
- A coluna de **abundância** da tabela de íons, a nova busca na biblioteca a partir da aba Candidates, e os filtros **Molecular ion** e **Hand-edited**.

**Corrigido**

- Os cinco atalhos de área de trabalho, que nunca tinham disparado: `Cmd+5` era lido como a tecla errada.
- A faixa de filtros da revisão desenhando sobre as contagens numa tela de portátil.
- A validação cruzada do modelo discriminante, que deixava a injeção retida influenciar a sua própria previsão e dava ao ruído um Q² respeitável.
- A correção de deriva saltando lotes com dois controles.
- Abrir um projeto com biblioteca grande derrubava o processo (um defeito upstream de `LargeListMessagePack`).
- As referências de regressão visual descreviam a máquina que as fez em vez da aplicação: as duas fontes agora vão no pacote.

**Alterado**

- A versão é carimbada uma vez, em `Directory.Build.props`, e mostrada em About.
- A pasta de configurações pode ser movida com `OPENDIAL_SETTINGS_DIR`.

## 0.1.0 — setembro de 2026

O primeiro port funcional: a camada aberta de dados brutos com o leitor de mzML e a ponte
msconvert, o leitor nativo de `.wiff` da SCIEX, o console construído para macOS, a aplicação de
desktop com as áreas Explorer, Analytics, Method e Samples, o ciclo de revisão com as cinco
marcações do MS-DIAL, reintegração manual e separação de isômeros, a interoperabilidade com o
OpenQuant, os componentes principais, o agrupamento e a rede molecular, e os testes sem tela e
visuais.

## Atualizar esta página

Toda versão atualiza o manual com ela: qualquer área de trabalho, controle, configuração, arquivo,
script ou armadilha conhecida nova ganha a sua frase na página a que pertence, esta página ganha as
suas notas, e os PDFs são reconstruídos, nos dois idiomas. Os testes do manual seguram a primeira
parte; a segunda é um hábito.
