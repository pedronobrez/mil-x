---
title: Caches e armazenamento
section: Data and files
order: 33
summary: O cache de varreduras de survey que torna imediato reabrir um projeto, o cache de mzML convertido, os links wiff-samples, e onde vivem as configurações.
---

# Caches e armazenamento

## O cache de varreduras de survey

Um arquivo de fabricante custa a mesma leitura toda vez que um projeto é aberto — cerca de 25
segundos para um `.wiff` de 60 MB do ZenoTOF — e uma passagem de revisão sobre oito deles começava
com três minutos de espera. Depois da primeira leitura, todo espectro de um arquivo é gravado num
cache, e o Explorer, a grade de picos, a árvore de canais e os espectros de produto vêm de lá:

| | |
| --- | --- |
| biblioteca do fabricante, um `.wiff` de 60 MB | 24 882 ms |
| o mesmo arquivo do cache | 87 ms |
| o que é guardado | 484 varreduras, 4 295 640 centroides, 32 MB |

É um cache de exibição, nunca uma entrada de processamento: uma corrida lê sempre o arquivo
original, de modo que nada quantitativo depende dele. As massas são guardadas como décimos de
milidalton num inteiro de 32 bits — duas ordens mais fino do que qualquer tolerância com que um
cromatograma é extraído — o que reduz à metade tanto o arquivo quanto o tempo de o ler.

As entradas são indexadas pela identidade do próprio arquivo — caminho, tamanho e data de
modificação — de modo que um arquivo editado ou substituído nunca serve um desatualizado. O
depósito é limitado a 8 GB e esvaziado pelo menos usado recentemente.

Vive em `~/Library/Caches/MIL-X/spectra` no macOS, `$XDG_CACHE_HOME/MIL-X/spectra` no Linux
e sob `LocalAppData\MIL-X\spectra` no Windows; `MILX_CACHE` move-o. [[settings]] mostra o
seu tamanho e esvazia-o. Apagar a pasta à mão é seguro: custa uma leitura lenta por arquivo.

## O cache de mzML convertido

Um arquivo de fabricante que passa pelo msconvert vira `<name>.mzML` ao lado da origem, ou dentro
da pasta de cache de conversão definida em [[settings]] (`MILX_MZML_CACHE` faz o mesmo). É
reutilizado enquanto for mais novo do que a origem; `MILX_MSCONVERT_FORCE=1` reconverte. Esses
arquivos são mzML completos e podem ser usados por qualquer outra coisa que leia mzML.

## Os links wiff-samples

Um `.wiff` multiamostra ganha uma pasta `wiff-samples` ao lado com um link simbólico por amostra
(`name.s2.wiff` com o seu `.wiff.scan`), porque o motor identifica uma injeção pelo seu caminho.
São recriados quando faltam e não custam nada; num compartilhamento que recusa links o arquivo é
copiado. Veja [[raw-data-formats#SCIEX .wiff]].

## O arquivo de configurações

`settings.json` na pasta de dados da aplicação — `~/.config/MIL-X` no macOS e Linux,
`%APPDATA%\MIL-X` no Windows, ou onde `MILX_SETTINGS_DIR` apontar. O que guarda está em
[[settings#O arquivo de configurações]].

## O que um projeto não guarda

Nada dos caches: um projeto movido para outra máquina lê os seus arquivos brutos de novo. Nada da
área de estatística: uma correção de deriva ou um modelo ajustado é recalculado a pedido e nunca
gravado. A curadoria é guardada, ao lado do alinhamento; veja [[projects-and-files]].
