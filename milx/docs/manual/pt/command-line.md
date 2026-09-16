---
title: A linha de comando e as ferramentas
section: Reference
order: 44
summary: O console milx-cli para processar sem a janela, os scripts de smoke e de comando, e as ferramentas de diagnóstico.
---

# A linha de comando e as ferramentas

## milx-cli

O console do próprio MS-DIAL (`MSDIALCUI`), construído com a camada de dados brutos do MIL-X e
publicado como binário autocontido por `scripts/build-cli.sh` em `dist/milx-cli-<rid>/`.
Corre o mesmo motor que a aplicação de desktop, sem a janela, que é o que um servidor ou um script
de lote quer.

```bash
milx-cli lcms -i /data/run1 -o /data/run1/out -m method.txt -p
```

| Opção | Significado |
| --- | --- |
| `lcms`, `gcms`, `dims`, `imms`, `lcimms`, `msn`, `eic` | o fluxo; a aplicação de desktop cobre `lcms` e `gcms` |
| `-i` | uma pasta (todo arquivo bruto nela), um arquivo, ou um CSV listando arquivos com a sua classe e tipo |
| `-o` | a pasta de saída |
| `-m` | o arquivo de método; veja [[method-parameters]] |
| `-p` | gravar também um `.mdproject`, que a aplicação de desktop e o MS-DIAL no Windows abrem |
| `--version` | a versão do MS-DIAL |

O lançador `milx-cli` força a cultura invariante antes de começar; use-o em vez de
`MSDIALCUI` diretamente. Os arquivos de fabricante são convertidos e os `.wiff` são lidos
nativamente exatamente como na aplicação, com o plugin em `plugins/sciex` ao lado do binário.

## Scripts

Todos sob `milx/scripts/`; veja [[building-and-testing]] para os de construção.

| Script | O que faz |
| --- | --- |
| `smoke-ui.py` | comanda a aplicação instalada e verifica que funciona: `--project` abre um projeto processado e verifica a janela; `--process FOLDER --library X.msp` constrói um projeto a partir dos arquivos brutos, processa-o, reintegra um pico, salva, exporta, abre o manual |
| `ui-drive.sh` | as mesmas primitivas uma de cada vez — `front`, `click X Y`, `key CODE cmd`, `shot PATH`, `where` — para uma captura de tela ou uma olhada |
| `build-manual.py` | constrói o PDF e o HTML deste manual a partir de `docs/manual`; `--lang pt` constrói a edição em português a partir de `docs/manual/pt` |
| `make-app-bundle.sh` | publica e empacota `dist/MIL-X.app` |
| `build-gui.sh`, `build-cli.sh` | publicam a aplicação e o console |
| `test.sh` | toda suíte de testes mais as corridas sintéticas ponta a ponta |
| `setup-macos.sh` | o SDK do .NET, sem sudo |
| `fetch-sciex-assemblies.sh` | o SDK da SCIEX para `vendor/sciex`, para o leitor nativo de `.wiff` |

## Ferramentas

Sob `milx/tools/`, corridas com `dotnet run --project tools/<name> -- …`:

| Ferramenta | O que faz |
| --- | --- |
| `msdial_param_to_method.py <param.txt> --out method.txt [--msp lib.msp]` | transforma uma exportação de parâmetros do MS-DIAL do Windows num arquivo de método, chave a chave, para que uma corrida possa ser reproduzida |
| `make_synthetic_mzml.py --out DIR [--samples N] [--mode gcms]` | um conjunto de dados sintético LC-MS/MS DDA (ou GC-MS EI) com a sua biblioteca e método, para os testes ponta a ponta |
| `ResultCompare <reference> <test> [rtTol] [mzTol] [out.tsv]` | casa dois resultados pico a pico e feature a feature e relata as razões; `--check-library <project> <lib.msp>` pergunta se uma biblioteca poderia ter produzido as anotações de uma corrida; `--dump-params <project>` imprime os parâmetros de um projeto; `--name-detail` lista as discordâncias de nome |
| `RawDump <file> [--full] [--max N]` | imprime o que a camada de dados brutos lê de um arquivo: espectros, precursores, tempos — a verificação de paridade aberto contra fechado |
| `WiffProbe <file.wiff> [cycles]` | imprime como uma aquisição `.wiff` está disposta: experimentos, ciclos, precursores por varredura |
| `make_icon.py` | desenha o ícone da aplicação |
