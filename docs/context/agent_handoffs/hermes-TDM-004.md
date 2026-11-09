# hermes/TDM-004

- Status: ready_for_finalizer
- Candidate commit: HEAD (the commit containing this handoff)
- Base inspected: a951e8b862fef891806f3e2aac422c2d2d186b19
- Scope: empacotamento local isolado para o AEU; nenhum arquivo de produto, migração histórica ou compose do proprietário alterado.
- Files changed: `Dockerfile`, `.dockerignore`, `.gitignore`, `docker-compose.aeu.yml`, `.env.aeu.example`, `docs/aeu/rodar-no-aeu.md`, `docs/context/agent_handoffs/hermes-TDM-004.md`.
- Validation: `com-postgres sh -c 'LANG=pt_BR.UTF-8 LC_ALL=pt_BR.UTF-8 dotnet test ThreeDManager.slnx'` passou no clone e em exportação Git sobre filesystem Linux sensível à caixa: 213 aprovados, 0 falhas, 0 ignorados. `dotnet build ThreeDManager.slnx --no-restore --nologo` no clone: 0 warnings, 0 errors. Build completo e publish Release do Web também passaram na exportação Linux. `git diff --check` passou. Compose v2.39.4 resolveu `config --format json` em cópia temporária com configuração sintética; asserções verificaram isolamento, porta loopback, dependência saudável, perfil migrate, volumes nomeados e senha compartilhada por interpolação. Senha ausente falhou explicitamente. `.env.aeu` confirmado ignorado pelo Git.
- Deployment impact: publish/update required (somente para disponibilizar este novo ambiente AEU; não publicar nem reiniciar o serviço Windows).
- Next action for finalizer: inspecionar `git diff a951e8b862fef891806f3e2aac422c2d2d186b19 HEAD -- Dockerfile .dockerignore .gitignore docker-compose.aeu.yml .env.aeu.example docs/aeu/ docs/context/agent_handoffs/hermes-TDM-004.md`, repetir os gates e encaminhar ao proprietário a sequência de migração/subida documentada, sem implantação automática.

## Desenho e operação

O alvo final web tem somente ASP.NET runtime, usuário app e porta 8080. O alvo
opcional migrate mantém SDK e dotnet-ef 10.0.8, executando a cadeia existente
explicitamente antes da primeira subida. Não há migração automática em Program.cs.
O compose padrão tem db/web; o serviço migrate só entra pelo perfil ou invocação
explícita. PostgreSQL 16 não publica porta e usa exclusivamente
`aeu_threedmanager_pgdata`; chaves Linux DataProtection persistem no volume
`aeu_threedmanager_keys`. A conexão usa a única senha Postgres gerada no exemplo.

## Não validado / limitações

Docker daemon indisponível e plugin Compose ausente neste container. Foi utilizado
um binário Compose v2.39.4 temporário apenas para validar a configuração, sem
alterar o host. Não foram executados docker build/up, healthcheck real, ownership
dos volumes, migração dentro da imagem, login na imagem nem persistência após
recriação. O aceite da TDM-004 reserva a subida real ao botão Executar do painel.

Tentativa adicional de EF update local com o publish/configuração Production,
fora de Docker e em PostgreSQL descartável, parou antes de conectar: o usuário do
ambiente não pode criar `/usr/share/ThreeDManager`. Nenhuma migração desse smoke
foi validada; login/dashboard desse smoke não foram executados. Não contornamos
com Testing/InMemory nem mudamos o produto: o Dockerfile prepara e atribui ao app
o diretório que Program.cs exige, mas a permissão na imagem aguarda teste real.

O botão Executar não prova que as migrações foram aplicadas. Até haver orquestração
pelo painel, o operador precisa executar o serviço migrate conforme o runbook.

## Para a memória

- A TDM-003 integrada corrigiu a portabilidade; esta rodada recriou clone e BASE
  como exigido após o desbloqueio, preservando o registro do bloqueio anterior.
- Program.cs persiste DataProtection em CommonApplicationData; em Linux é
  `/usr/share/ThreeDManager/keys`. Preparar o diretório é necessário para não-root,
  inclusive na execução design-time do EF, não só ao servir HTTP.
- Program.cs não aplica migrações. O caminho AEU é explícito e separado da imagem
  runtime, sem modificar o produto nem o banco do proprietário.
- `env_file` não substitui `--env-file` na interpolação Compose; o comando precisa
  ambos para usar um único POSTGRES_PASSWORD gerado. Não registrar config expandido
  com credenciais reais; a evidência desta rodada usa somente fixture sintética.

- Compose do app: docker-compose.aeu.yml
