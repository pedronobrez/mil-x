---
title: Configurações
section: Reference
order: 40
summary: A janela Settings — tema, o cache de varreduras de survey, a ponte msconvert — e o arquivo de configurações por trás dela.
---

# Configurações

**File ▸ Settings…** abre uma janela pequena com três seções. **Save** grava-as e aplica o tema;
**Cancel** deixa tudo como estava. O caminho do arquivo de configurações é mostrado no fundo.

## Appearance

**Theme**: **Follow the system**, **Light** ou **Dark**. A interface inteira segue, gráficos
incluídos. `OPENDIAL_THEME=dark` ou `light` força um para uma sessão sem tocar na configuração.

## Survey scan cache

Ler um arquivo de fabricante demora o mesmo toda vez que é aberto, de modo que os espectros de cada
arquivo são guardados aqui depois da primeira leitura e reabrir um projeto desenha os seus
cromatogramas de imediato. A linha mostra quantos arquivos estão em cache e quanto espaço ocupam,
com a pasta; **Empty it** apaga-os. Nada mais é guardado, e esvaziá-lo só custa uma leitura lenta
por arquivo. Veja [[caches-and-storage]].

## Vendor format conversion

Thermo `.raw`, `.d` da Agilent e Bruker, `.lcd` da Shimadzu e `.wiff2` da SCIEX — e `.wiff` quando
o leitor nativo não está presente — são convertidos para mzML com o msconvert do ProteoWizard
antes do processamento.

| Configuração | Significado |
| --- | --- |
| **msconvert path** | um executável msconvert nativo; vazio significa "procurar no PATH" |
| **Use Docker instead of a native msconvert** | correr a imagem Docker do ProteoWizard |
| **Docker image** | que imagem; a padrão traz os leitores dos fabricantes sob Wine |
| **Conversion cache** | para onde vão os arquivos mzML convertidos; vazio significa ao lado do arquivo de origem |

Veja [[raw-data-formats#A ponte msconvert]] para o que cada caminho precisa.

## O arquivo de configurações

`settings.json` sob `~/.config/OpenDIAL` no macOS e Linux, `%APPDATA%\OpenDIAL` no Windows, ou a
pasta que `OPENDIAL_SETTINGS_DIR` nomeia. Além das três seções acima, lembra o que a aplicação
aprende com o uso:

| Campo | O que lembra |
| --- | --- |
| `RecentProjects` | os últimos dez projetos, para o menu File |
| `LastInputFolder`, `LastOutputFolder`, `LastProjectFolder` | onde os diálogos de arquivo abrem |
| `LastMspFile`, `LastMethodFile` | a biblioteca e o método a oferecer ao próximo projeto |
| `ShowLog` | se o painel de log estava aberto |
| `IonTableColumnOrder` | a ordem em que o revisor arranjou as colunas da tabela de íons |
| `IonTableDetached` | se a tabela de íons estava destacada numa janela própria |
| `HelpLanguage` | o idioma em que o manual foi lido pela última vez, `en` ou `pt` |

Um arquivo corrompido é ignorado e os padrões usados. Apagá-lo repõe tudo o que está acima.
