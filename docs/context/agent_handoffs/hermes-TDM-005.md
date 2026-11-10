# hermes/TDM-005

- Status: ready_for_finalizer
- Candidate commit: HEAD (the commit containing this handoff)
- Base inspected: a951e8b862fef891806f3e2aac422c2d2d186b19 (origin/main; inclui TDM-003)
- Scope: separar falha esperada de leitura da persistência na importação, sem schema novo.
- Files changed:
  - `src/ThreeDManager.Web/Controllers/PrintImportsController.cs`
  - `tests/ThreeDManager.Tests/PrintImportFailureTests.cs`
  - `docs/context/agent_handoffs/hermes-TDM-005.md`
- Validation:
  - `com-postgres sh -c 'LANG=pt_BR.UTF-8 LC_ALL=pt_BR.UTF-8 dotnet test ThreeDManager.slnx'` na raiz do clone: 217 aprovados, 0 falhas, 0 ignorados.
  - Mesmo gate numa exportação limpa de BASE com somente os dois arquivos de código/teste candidatos em `/opt/data/cache/scratch/tdm005-996814-linux`: 217 aprovados, 0 falhas, 0 ignorados. Sonda CaseProbe/caseprobe confirmou sensibilidade à caixa antes da extração. Isso revalida o finding do parecer antigo sem mudar arquivos de portabilidade.
  - `LANG=pt_BR.UTF-8 LC_ALL=pt_BR.UTF-8 dotnet build ThreeDManager.slnx` nos dois ambientes: compilação com êxito, 0 avisos, 0 erros.
  - `dotnet format ThreeDManager.slnx --verify-no-changes --no-restore --include src/ThreeDManager.Web/Controllers/PrintImportsController.cs tests/ThreeDManager.Tests/PrintImportFailureTests.cs`: exit 0 após corrigir apenas espaçamento no teste novo; `git diff --check`: exit 0.
  - Smoke real Kestrel em loopback, Testing/InMemory isolado: upload → Process → Details, 302/302/200; arquivo com duração fora do intervalo exibe erro amigável persistido sem detalhes internos; arquivo válido é processado com avisos. Aplicação encerrada depois da prova. Evidência externa: `rodada-smoke.log`.
- Deployment impact: publish/update required (publicação necessária; não executada).
- Next action for finalizer: inspecionar `git diff a951e8b862fef891806f3e2aac422c2d2d186b19 HEAD`, repetir gates e smoke e aguardar aprovação Hermes/Codex para o SHA exato antes de integrar pelo AEU.

## Prova vermelho → verde

Logs externos em `/trabalho/threedmanager/TDM-005/`, todos com prefixo `rodada-` para distingui-los de tentativas anteriores:

1. `red-parser.log`: teste com GCodePrintFileParser real falha porque o código antigo grava `Value was either too large...` em vez da mensagem amigável. `green-parser.log`: 1/1 aprovado após restringir a recuperação ao overflow e limpar metadados antigos.
2. `red-database.log`: interceptor lança DbUpdateException só na primeira gravação; teste falha porque nenhuma exceção propaga (captura antiga tentava salvar novamente). `green-database.log`: 2/2 aprovados após retirar a persistência da captura.
3. `red-json.log`: 1 falha/3 aprovados; fronteira compilada captura `object` em vez de JsonException. JSON malformado já tinha retorno amigável e esse comportamento é caracterizado separadamente. A alteração restringe o catch; suíte final aprova os quatro testes novos.

Saída real final no checkout sensível à caixa (`rodada-linux-test-build.log`):

```text
Aprovado!  – Com falha:     0, Aprovado:   217, Ignorado:     0, Total:   217, Duração: 14 s - ThreeDManager.Tests.dll (net10.0)
Compilação com êxito.
    0 Aviso(s)
    0 Erro(s)
```

.NET/xUnit não emite as linhas unittest `Ran N tests` e `OK`; não foram inventadas.

## Decisões, limites e riscos

- Só OverflowException da chamada Parse é recuperável neste lote: o parser concreto usa regex numérica seguida de conversão de duração; overflow foi reproduzido com conteúdo sintético real. Não capturar FormatException especulativamente, nem erros de programação, banco ou serialização.
- Falha de persistência propaga, inclusive ao salvar o erro recuperável. O tratador global é o existente; não há nova infraestrutura nem retry.
- JSON inválido conserva o aviso existente. O teste da fronteira usa ExceptionHandlingClauses do método compilado: verifica precisamente o tipo capturado, mas não injeta falha de runtime na desserialização. O DTO não oferece conversor injetável e não se adiciona gancho de produção para isso.
- Não houve mudança de parser, views, login, dependências, migração ou dados históricos. Mensagens internas já persistidas não são saneadas.
- com-postgres foi usado para todos os gates, mas esta base mantém a suíte e o smoke em InMemory; não há alegação de cobertura relacional nem acesso ao banco do proprietário. Auditoria NuGet não executada: não é gate declarado desta tarefa e nenhuma dependência mudou.
- Sem identificador de correlação ou logger novo. Nenhum texto de exceção é persistido pelo tratamento recuperável.
- Após desbloqueio, o protocolo mandou recriar repo/BASE: esta é uma nova implementação sobre main corrigida, não merge/rebase do candidato antigo. Logs sem prefixo `rodada-` são históricos e não evidência desta entrega.

## Para a memória

A exceção transitória de gravação deve falhar só uma vez no teste: caso contrário a segunda gravação indevida do código antigo também lança e pode esconder a reclassificação como erro de arquivo. Limpar ParsedDataJson ao falhar o reprocessamento evita reter metadados antigos. Validar em filesystem comprovadamente sensível à caixa e partir da main que incorpora a portabilidade elimina o bloqueio MSB3202 sem ampliar o escopo de TDM-005.
