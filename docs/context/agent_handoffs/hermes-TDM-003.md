# hermes/TDM-003

- Status: ready_for_finalizer
- Candidate commit: HEAD (the commit containing this handoff)
- Base inspected: a92625daba8afd9d58b18dd53b3c76b160777426 (origin/main)
- Scope: corrigir somente a caixa das três referências ao projeto Infrastructure para permitir restore/build em Linux, preservando o arquivo e o assembly existentes.
- Files changed:
  - `ThreeDManager.slnx`
  - `src/ThreeDManager.Web/ThreeDManager.Web.csproj`
  - `src/ThreeDManager.Worker/ThreeDManager.Worker.csproj`
  - `docs/context/agent_handoffs/hermes-TDM-003.md`
- Validation:
  - Cópia isolada em `/opt/data/cache/scratch/tdm003-linux/repo`; verificado que criar `CaseProbe` não cria `caseprobe` nesse disco (sonda removida).
  - Antes da correção, `LANG=pt_BR.UTF-8 LC_ALL=pt_BR.UTF-8 dotnet test ThreeDManager.slnx` na cópia Linux falhou com MSB3202: projeto Infrastructure não encontrado. Depois foram copiadas somente as três configurações corrigidas para a mesma cópia.
  - Depois da correção, o mesmo comando na cópia Linux passou: `Aprovado!  – Com falha:     0, Aprovado:   213, Ignorado:     0, Total:   213, Duração: 14 s - ThreeDManager.Tests.dll (net10.0)`.
  - `LANG=pt_BR.UTF-8 LC_ALL=pt_BR.UTF-8 dotnet build ThreeDManager.slnx` na cópia Linux: `Compilação com êxito.`, `0 Aviso(s)`, `0 Erro(s)`, incluindo o Worker.
  - `LANG=pt_BR.UTF-8 LC_ALL=pt_BR.UTF-8 dotnet test ThreeDManager.slnx` na raiz do clone: `Aprovado!  – Com falha:     0, Aprovado:   213, Ignorado:     0, Total:   213, Duração: 35 s - ThreeDManager.Tests.dll (net10.0)`.
  - A busca Git pelo nome incorreto original não encontra ocorrências; `git diff --check` passa.
- Deployment impact: none (nenhum). Não houve publicação, mudança de assembly, namespace, migração ou serviço instalado.
- Next action for finalizer: inspecionar `git diff a92625daba8afd9d58b18dd53b3c76b160777426 HEAD` e repetir os gates no fluxo de integração do AEU após as duas revisões.

## Limites e riscos

Nenhum código funcional, teste ou dependência foi alterado. A própria suíte existente reproduz a falha de configuração antes da correção. Smoke da aplicação não executado: não há mudança de interface ou comportamento de execução. Docker não está acessível neste container; o banco operacional não foi acessado e não é necessário para esta validação (213 testes passam sem ele). Nenhum comando EF foi executado, pois não há alteração de schema. Diário reservado ao integrador conforme PROJETO.md.

## Para a memória

O nome versionado do projeto Infrastructure tem `m` minúsculo em `ThreeDmanager`; mantê-lo preserva o nome do assembly. A solução e as referências Web/Worker precisam usar a mesma caixa. Uma cópia em filesystem sensível à caixa reproduz MSB3202 antes da correção; após ela, o build completo e os 213 testes passam com cultura pt-BR. Não há motivo nesta tarefa para renomear projeto, assembly ou namespace.
