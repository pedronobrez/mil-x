---
title: Variáveis de ambiente
section: Reference
order: 43
summary: Toda variável OPENDIAL_* — o que muda, para quem é — mais as que o runtime lê.
---

# Variáveis de ambiente

Nenhuma delas é necessária no uso comum; a janela Settings cobre o que uma pessoa muda. Existem
para scripts, diagnóstico e o smoke test. No macOS uma aplicação lançada pelo Finder não vê as
variáveis de um shell; lance-a com `open -a /Applications/OpenDIAL.app --env NAME=value` ou a
partir de um terminal.

## Armazenamento e aparência

| Variável | Efeito |
| --- | --- |
| `OPENDIAL_SETTINGS_DIR` | a pasta de `settings.json` em vez de `~/.config/OpenDIAL` |
| `OPENDIAL_CACHE` | a raiz do cache de varreduras de survey em vez de `~/Library/Caches/OpenDIAL` (o cache vive em `spectra` dentro dela) |
| `OPENDIAL_THEME` | `dark` ou `light` para esta sessão, ignorando a configuração |

## Dados brutos

| Variável | Efeito |
| --- | --- |
| `OPENDIAL_WIFF_CENTROID` | como o leitor nativo de `.wiff` centroida os espectros de perfil: `sciex` (padrão, o detector de picos do fabricante), `msdial` (máximo local), `0` (manter perfil) |
| `OPENDIAL_WIFF_SAMPLE` | que amostra de um `.wiff` multiamostra ler quando nada mais o diz: um índice (base 0) ou um nome de amostra |
| `OPENDIAL_WIFF_MIN_INTENSITY` | descartar picos centroidados abaixo desta intensidade |
| `OPENDIAL_PLUGINS` | uma pasta extra de onde carregar plugins de leitura de arquivos brutos, além de `<app>/plugins` |
| `OPENDIAL_LEGACY_RAWDATA_DLL` | o caminho da dll de leitura original do MS-DIAL, para `.ibf`, `.cdf` e `.imzML`, em vez de `<app>/plugins/legacy` |

## A ponte msconvert

| Variável | Efeito |
| --- | --- |
| `OPENDIAL_MSCONVERT` | o executável msconvert; `docker` força o caminho Docker |
| `OPENDIAL_DOCKER` | o executável docker (padrão `docker`) |
| `OPENDIAL_PWIZ_IMAGE` | a imagem do ProteoWizard (padrão `proteowizard/pwiz-skyline-i-agree-to-the-vendor-licenses:latest`) |
| `OPENDIAL_DOCKER_PLATFORM` | a opção `--platform` (padrão `linux/amd64`, de que o Apple Silicon precisa) |
| `OPENDIAL_MZML_CACHE` | para onde vão os arquivos mzML convertidos em vez de ao lado da origem |
| `OPENDIAL_MSCONVERT_ARGS` | argumentos extra do msconvert, acrescentados depois dos padrões |
| `OPENDIAL_MSCONVERT_PROFILE` | `1` mantém os dados de perfil (sem detecção de picos do fabricante); ponha o método em Profile |
| `OPENDIAL_MSCONVERT_FORCE` | `1` reconverte mesmo quando existe um mzML em cache |

Os valores da janela Settings têm precedência sobre estas na aplicação de desktop.

## Comandar a aplicação por script

Estas fazem a aplicação fazer algo no arranque, para scripts e para os testes; veja
[[building-and-testing#O smoke test]].

| Variável | Efeito |
| --- | --- |
| `OPENDIAL_OPEN` | abrir este `.odproj`, `.mdproject` ou pasta de resultados |
| `OPENDIAL_OPEN_RAW` | adicionar este arquivo bruto, ou todo arquivo bruto desta pasta, e abrir o primeiro no Explorer |
| `OPENDIAL_IMPORT_OPENQUANT` | importar este lote `.oqproj` |
| `OPENDIAL_EXPORT_OPENQUANT` | depois de `OPENDIAL_OPEN`, gravar as features anotadas como componentes do OpenQuant neste caminho |
| `OPENDIAL_AUTORUN` | uma pasta de arquivos brutos (com `library.msp` e `method*.txt` opcionais): processá-la para `opendial_output` no arranque |
| `OPENDIAL_SNAPSHOT` | renderizar a janela para este PNG depois de `OPENDIAL_SNAPSHOT_DELAY` segundos (padrão 10), na área de trabalho `OPENDIAL_SNAPSHOT_TAB` (0–4), depois de `OPENDIAL_SNAPSHOT_ACTION` (`explorer-peak`, `explorer-channel`, `analytics-spectrum`, `analytics-metric`, `analytics-magnify`, `stats-cluster`, `stats-network`, `search-demo`, `reintegrate-demo`, `review-peaks`, `review-map`, `review-candidates`, `wizard`, `about`) |
| `OPENDIAL_UI_PROBE` | gravar o que a janela está mostrando neste arquivo JSON sempre que muda, e vigiar `<file>.commands` por comandos; o canal do smoke test |
| `OPENDIAL_TRACE` | `1` imprime no console os avisos de binding e layout do Avalonia |

## Testes

| Variável | Efeito |
| --- | --- |
| `OPENDIAL_UPDATE_BASELINES` | `1` reescreve as referências de regressão visual a partir da renderização atual |
| `OPENDIAL_TEST_ALIGNMENT` | um resultado de alinhamento real para os testes de estatística com dados reais |
| `OPENDIAL_TEST_WIFF` | um `.wiff` real para os testes do plugin da SCIEX |

## O runtime

| Variável | Efeito |
| --- | --- |
| `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT` | `1` — definida pelo lançador da construção de linha de comando; a aplicação de desktop força ela própria a cultura invariante. Veja [[troubleshooting#Processamento]] |
| `DOTNET_ROOT` | onde está o SDK do .NET, para construir (`~/.dotnet` depois de `setup-macos.sh`) |
