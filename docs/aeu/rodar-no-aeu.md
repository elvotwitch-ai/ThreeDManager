# ThreeDManager no AEU

Este ambiente é isolado do serviço Windows e do banco do proprietário. O código do
produto não muda: somente empacotamento e configuração. Requer Docker Engine e
Docker Compose v2 no host de execução do painel.

## Configuração privada

Na primeira execução, o painel cria `.env.aeu` a partir de `.env.aeu.example` e
substitui cada `__GERAR__` por um valor aleatório independente. Não use o exemplo
literalmente para subir a aplicação. Não versione nem imprima `.env.aeu`.

- Banco: `POSTGRES_DB=threedmanager_aeu`, usuário `POSTGRES_USER=aeu_app`.
- Login da aplicação: `AlphaAccess__Username=operador-aeu`; a senha é o valor
  privado gerado para `AlphaAccess__Password`, não a senha do banco.
- `POSTGRES_PASSWORD` aparece uma única vez no exemplo; o compose monta
  `ConnectionStrings__Default` usando esse mesmo valor. `env_file` alimenta os
  containers; **`--env-file .env.aeu` é obrigatório** para a interpolação `${...}`
  pelo Compose. Não substitua por `.env` e não exporte valores conflitantes no shell.
- Os `appsettings*.json` e perfis locais ficam fora do contexto de build para não
  empacotar configuração do proprietário. O runtime usa as variáveis do compose.

## Primeira subida e migrações

`Program.cs` não chama `Database.Migrate()` nem `MigrateAsync()`. Por isso, antes
de abrir a aplicação, execute a sequência abaixo na raiz do clone de execução,
com `.env.aeu` já gerado pelo painel:

```sh
docker compose --env-file .env.aeu -f docker-compose.aeu.yml build web migrate
docker compose --env-file .env.aeu -f docker-compose.aeu.yml up -d --wait db
docker compose --env-file .env.aeu -f docker-compose.aeu.yml --profile migrate run --rm migrate
docker compose --env-file .env.aeu -f docker-compose.aeu.yml up -d web
```

**Só continue após cada comando terminar com sucesso.** O serviço opcional
`migrate` usa o alvo SDK do Dockerfile, `dotnet-ef` 10.0.8 e a cadeia existente de
migrações, contra `db:5432`. A imagem final `web` contém somente o runtime ASP.NET
e o publish, sem SDK nem ferramenta EF. O perfil não sobe no `up` normal.

O botão Executar sobe o compose, mas não representa confirmação de migração.
Enquanto o painel não orquestrar o passo de migração, o operador deve executá-lo
explicitamente antes de usar o app. Não acrescentamos migração automática ao
produto e não criamos nem alteramos migrações nesta tarefa.

Após integrar novas migrações, pare `web`, repita o serviço `migrate`, confirme
sucesso e recrie `web`. Isso só vale para o banco descartável do AEU; migração no
banco real e publicação do serviço Windows continuam sendo ações do proprietário.

## Entrar e conferir

Abra `http://127.0.0.1:8800/Account/Login` no host. Entre como `operador-aeu`, com
a senha privada gerada para a aplicação. A raiz deve exigir login; após entrar,
confira o dashboard e uma listagem, por exemplo `/Products`, sem erro de tabela
inexistente. Não use dados reais de produção nesta instância.

Apenas `web` publica porta: `127.0.0.1:8800` no host para `8080` no container.
O banco não publica porta. Isto não configura acesso externo, domínio, HTTPS,
túnel ou acesso pelo celular. Não exponha esta configuração HTTP na internet.

```sh
# Estado; evita imprimir a configuração expandida com os segredos.
docker compose --env-file .env.aeu -f docker-compose.aeu.yml ps
# Parar, preservando dados e chaves:
docker compose --env-file .env.aeu -f docker-compose.aeu.yml stop
# Remover containers e rede próprios, ainda preservando os volumes:
docker compose --env-file .env.aeu -f docker-compose.aeu.yml down
```

## Persistência e limites

- Projeto Compose: `aeu-threedmanager`; sem `container_name` ou rede externa.
- PostgreSQL 16 usa exclusivamente o volume nomeado `aeu_threedmanager_pgdata`.
  É descartável, mas `stop` e `down` não o apagam. Não use `down -v` sem querer
  descartar todos os dados **desta** instância.
- `aeu_threedmanager_keys` persiste as chaves DataProtection no caminho Linux
  que o programa já usa (`/usr/share/ThreeDManager/keys`). A imagem prepara esse
  diretório para o usuário `app` não-root. No Linux essas chaves não recebem a
  proteção DPAPI do Windows: proteja o host e o volume; não os exporte.
- Não monte diretórios do host, não conecte à rede do proprietário e não use
  `docker-compose.yml`, porta `5436` ou container/volume `threedmanager-db`.
- Trocar `POSTGRES_PASSWORD` em um volume já inicializado não altera a senha do
  usuário no PostgreSQL. Não gere outro `.env.aeu` sobre dados existentes; se
  perder as credenciais deste banco descartável, o proprietário decide entre
  recuperar ou descartar somente o volume AEU.

## Validação e pendência de execução

A validação da tarefa cobre a suíte .NET, build/publish Linux e resolução do
compose com dados sintéticos. A subida real do compose, ownership dos volumes,
healthcheck, login na imagem e persistência após recriar containers ainda precisam
ser conferidos pelo botão Executar no host com Docker. Testar o publish local não
substitui essa conferência. Não houve implantação do serviço Windows.

## Registro da mudança

- 2026-11-09 — TDM-004: Dockerfile multi-stage não-root, compose AEU isolado, exemplo de
  configuração privada, migração explícita e instruções de operação. Objetivo:
  permitir o teste local sem tocar no banco ou na implantação do proprietário.
