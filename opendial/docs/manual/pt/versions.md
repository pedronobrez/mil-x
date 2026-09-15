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

## 0.9.0 — setembro de 2026

A versão atual. Segue o MS-DIAL 5.5.260817.

**Novo**

- **Um projeto para um lote adquirido das duas formas.** A área Samples tem uma coluna
  **Polarity**, adivinhada pelo nome de cada arquivo e editável. Quando o lote tem as duas,
  **Process batch** corre duas vezes — positivas para `positive/`, negativas para `negative/`,
  cada uma com o seu modo de íon — e emparelha os dois resultados sozinho. Um lote de uma
  polaridade só corre exatamente como corria.
- **A estatística passa a ver compostos em vez de íons.** Com uma polaridade vinculada, todo modelo
  é calculado sobre as duas corridas conciliadas: um composto que as duas viram aparece uma vez,
  com os números da corrida que o mediu melhor, nas injeções que as duas compartilham. Uma matriz
  em que metade das linhas são cópias da outra metade ajusta todo modelo sobre uma mentira. Veja
  [[polarity-merge]].

## 0.8.0 — setembro de 2026

Segue o MS-DIAL 5.5.260817.

**Novo**

- **As duas polaridades de um composto, lado a lado.** Uma nova aba de evidência, **Other
  polarity**, espelha o espectro de produto desta corrida contra o da outra para o mesmo composto,
  com o nome do parceiro, o m/z, o aduto, o tempo de retenção, o sinal-ruído e a correlação entre
  os dois perfis de altura logo abaixo. Com **Split evidence** ligado, MS/MS à esquerda e Other
  polarity à direita é o arranjo para o qual se vincula uma polaridade — e a segunda coluna agora
  a seleciona sozinha.
- **Um composto, um veredito.** **Tag both polarities**, ligado por padrão assim que uma
  polaridade é vinculada, leva a revisão através do par: confirmar um composto aqui confirma-o na
  outra corrida, e o desfazer toma de volta nas duas. As duas revisões são gravadas por **Save
  review**, cada uma no seu `_tags.xml`. Veja [[polarity-merge]].

## 0.7.0 — setembro de 2026

Segue o MS-DIAL 5.5.260817.

**Novo**

- **As duas polaridades de um lote, reconciliadas.** **Link polarity…** na barra da revisão
  emparelha o resultado na tela com o mesmo lote corrido do outro modo. Os compostos são casados
  pela molécula neutra — a mesma molécula é `[M+H]+` aqui e `[M-H]-` lá — dentro de 0,01 Da, dentro
  de 0,1 minuto, e só quando os seus perfis de altura ao longo das injeções concordam, o que é um
  sinal forte porque as duas corridas são as mesmas amostras. A tabela de íons ganha **Neutral**,
  **Pol**, **Other polarity** e **Quantify**, e a barra de filtros uma caixa de polaridade. As
  alturas nunca são somadas entre as polaridades: o lado que mediu melhor um composto quantifica-o
  e o outro confirma-o. O emparelhamento é gravado ao lado do alinhamento como
  `_polarity-pairs.json` e um resultado vinculado abre vinculado. Veja [[polarity-merge]].
- **A massa neutra na tabela de íons**, preenchida a partir do aduto haja ou não polaridade
  vinculada. A tabela de adutos de onde vem conhece os íons de carga dupla e os dímeros, não só os
  de carga simples.

## 0.6.0 — setembro de 2026

Segue o MS-DIAL 5.5.260817.

**Novo**

- **A biblioteca é lida, não suposta.** A área Method diz o que o `.msp` escolhido traz — quantos registros, que adutos, que faixa de massa e de tempo — e avisa quando os tempos de retenção dele são de outro gradiente. **Ignore its retention times** deixa-os fora da pontuação; **Calibrate to this run…** ajusta-os aos tempos que esta corrida mediu para o que nomeou, grava uma cópia calibrada ao lado da original e aponta o método para ela. Veja [[annotation#Por que tão poucas features são nomeadas]].
- **O tempo de retenção sai da pontuação de anotação por padrão.** Quase toda biblioteca pública traz tempos do gradiente para o qual foi feita, e pontuar com eles descarta casamentos espectrais corretos: no lote de fígado foi a diferença entre 286 e 1198 nomes. Religue quando a biblioteca tiver sido medida no método que está rodando.
- **Um composto, não vários íons.** Os adutos, isótopos, fragmentos de fonte e dímeros de um composto são reunidos atrás do íon que a corrida mediu melhor: eluem juntos, as alturas sobem e descem juntas entre as injeções, e a distância entre as massas é uma que um par de adutos dá. A tabela de íons diz o que cada linha é na coluna **Ion of**, e **One row per compound** na faixa de filtros esconde o resto.
- **Blank %** na tabela de íons e nos filtros: a altura média nas injeções do tipo Blank contra a média nas amostras. É o primeiro corte de qualquer corrida não dirigida, e a resposta para uma célula vermelha de mapa de calor num solvente.
- **Desfazer na revisão** (`⌘Z`, `⌘⇧Z`, e o menu Edit). Um **Confirm all shown** inteiro sobre um filtro de duas mil features volta num passo só, e a linha de estado diz o que foi desfeito.
- **Run report…** escreve a corrida: as contagens, a biblioteca, as injeções, o método, o log e as figuras da tela, num arquivo HTML com as figuras em vetor embutidas. Abre no navegador e imprime em PDF.

**Mudou**

- O espelho mantém a largura quando a área de evidência está dividida: abaixo da largura em que os dois cabem, a tabela de scores se afasta.

**Corrigido**

- Uma corrida de oito arquivos informava que não lera nenhum deles nativamente, enquanto o próprio log mostrava o leitor SCIEX em todos. A contagem era varrida do log da corrida, que guarda só as últimas milhares de linhas, e oito minutos de progresso de alinhamento empurraram o começo da corrida para fora dele. Agora a corrida conta enquanto as linhas chegam.

## 0.5.0 — setembro de 2026

Segue o MS-DIAL 5.5.260817.

**Novo**

- A página **Two factors** da área Statistics: a análise de variância de dois fatores por feature com a interação (somas de quadrados do tipo II, de modo que desenhos desbalanceados e células vazias são tratados), e ASCA sobre a matriz inteira com um teste de permutação por efeito e um gráfico de escores por efeito. O segundo fator é uma coluna **Factor** na área Samples, salva com o projeto. Veja [[two-factor-analysis]].
- O smoke test pousa numa feature cujo espectro está espelhado contra a biblioteca, confere que o painel diz isso, e grava esse espelho como figura; o `selectFeature` da sonda aceita `where: "mirrored"` e espera o espectro carregar, o que não fazia antes. Um projeto sem MS/MS faz o passo se afastar em vez de falhar.
- Seis coisas que a primeira passagem completa de revisão pediu. **Seguinte** segue a ordem em que a tabela de íons está ordenada, de modo que uma passagem descendo a coluna de S/N confirma descendo a tela em vez de saltar por id. **Reject all shown** fica ao lado de Confirm all shown. A faixa de filtros ganha **S/N ≥**. **Split evidence** põe dois conjuntos de abas de evidência lado a lado — o espelho ao lado das estatísticas da classe —, cada um na sua aba. Os gráficos respondem ao trackpad: dois dedos para o lado caminham pelo eixo, `⌘` com dois dedos estica o eixo de intensidade, e um duplo clique devolve os dois. E os dois grafos, a rede molecular e o mapa de vias, deixaram de ser figuras: a roda amplia em torno do ponteiro, o fundo arrasta, um nó da rede pode ser puxado para um lugar mais claro, e um duplo clique deixa tudo plano de novo.
- Uma figura sai da aplicação com a cara que a figura quer, não com a da janela. **Export…**, e **Export as a picture…** no menu de botão direito que todo gráfico agora tem, abrem um só diálogo: formato, resolução, **tema** — claro, escuro, ou como na tela, abrindo em claro para que uma sessão escura ainda dê uma figura clara —, fundo (o papel do tema, branco, ou nada) e o tamanho do texto. A pré-visualização é a própria exportação, e as escolhas ficam guardadas. Veja [[chart-export]].
- Todo gráfico do Explorer e da Analytics pode ser exportado, não só os da área Statistics: o cromatograma, o espectro, os painéis de pico, o espelho, o envelope isotópico, o mapa de features e as barras de abundância.
- O manual em português, ao lado do inglês: **Português** na janela de ajuda, e um segundo PDF.

**Alterado**

- A área Analytics abre no que confirmar um analito exige: a tabela de íons, o pico em toda amostra e o espectro de produto, tudo na tela de uma vez. A grade de picos é um painel próprio acima das abas de evidência em vez da primeira aba, e MS/MS é a aba mostrada por padrão; um divisor entre os dois define as alturas. O tamanho da grade é um único controle **Grid** `3 × 2`.
- A janela da tabela de íons reencaixa ao ser arrastada sobre a parte esquerda da janela principal: o lugar que vai ocupar acende quando a barra de título o cruza, e soltar aí encaixa-a. Enquanto a tabela está fora, os picos e o espectro ocupam toda a largura; a coluna dela não fica mais vazia.

- As teclas de revisão são `⌘⇧` (`Ctrl+Shift`) com um dígito para uma marcação, com C e X para os vereditos, com N para a próxima feature não revisada e com as setas para a seguinte e a anterior. Estão ligadas nas janelas, de modo que funcionam seja qual for o foco, na janela principal enquanto Analytics está na tela e na janela própria da tabela de íons.

**Corrigido**

- A rede molecular testava o clique contra as coordenadas do próprio layout em vez das do painel, de modo que num painel que não tivesse exatamente o tamanho do layout um nó não podia ser selecionado.
- Os rótulos *measured* e *reference* do espelho escolhiam o canto pela altura dos picos, que não é o que colide com eles: o m/z escrito acima de um pico é várias vezes mais largo que o pico. Cada rótulo vai agora para onde a sua metade do gráfico não tem nada desenhado.
- Uma célula de mapa de calor num branco podia aparecer vermelha sem dizer por quê. Uma linha padronizada só compara uma injeção com o resto da própria linha, de modo que uma feature que é ruído em todo lugar ainda tem a sua célula mais vermelha; pairar sobre a célula agora dá também o número a partir do qual a cor foi padronizada.
- Um PNG de um gráfico com título ou rótulo de eixo desenhava os dois no tamanho errado acima de 1×: um título com o dobro da altura, o rótulo do eixo fora da borda, enquanto tudo à volta escalava certo. A imagem é agora desenhada pelo mesmo código da tela e do SVG, em toda resolução.
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
