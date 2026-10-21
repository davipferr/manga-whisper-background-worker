# Mapa de funcionalidades: Manga Whisper Background Worker

O mapa do projeto: como instalar e rodar, como acionar cada funcionalidade, onde está o código e qual teste a cobre. Um agente que for mexer no projeto, verificar uma entrega ou investigar um bug começa por aqui, em vez de adivinhar. Caminhos a partir da raiz do repositório. Escrito pela skill `onboard`; a etapa `implement` do pipeline o mantém em dia.

Este repositório é metade do sistema: a API, o front e o esquema do banco ficam em `../manga-whisper` (com o próprio `docs/FEATURE-MAP.md`). Os dois só se falam pelo banco: a API cria jobs e pedidos de execução, este worker os executa.

## Instalar e rodar
- Requisitos: .NET SDK 10 (verificado com 10.0.102); PostgreSQL com o esquema de `../manga-whisper/database/init/`; Chrome local (fora do Docker) ou a imagem Debian com `chromium` + `chromium-driver` (no Docker)
- Rodar com o sistema todo: em `../manga-whisper`, `docker compose up --build` (serviço `worker`, `shm_size: 1gb`); logs: `docker compose logs -f worker`. **Não verificado: o Docker Desktop não estava rodando no onboard (2026-10-20)**
- Rodar fora do Docker: `docker compose up db` em `../manga-whisper`; copiar `src/MangaWhisper.BackgroundWorker.Api/.env.example` para `.env` apontando `DefaultConnection` para `localhost`; `dotnet run --project src/MangaWhisper.BackgroundWorker.Api`. Não verificado (precisa do banco)
- Build: `dotnet build MangaWhisper.BackgroundWorker.sln` → "Compilação com êxito", 0 avisos, 0 erros (verificado)
- Testes: **não há** (nenhum projeto de teste)

## Arquitetura em uma linha por camada
- `src/MangaWhisper.BackgroundWorker.Api/Program.cs`: DI, `DbContext`s, repositórios, serviços e os dois `BackgroundService`s.
- `Infrastructure/Services/`: `JobRunnerBackgroundService` (laço de 30 s) e `WorkerHeartbeatBackgroundService` (30 s). `Infrastructure/Repositories/`, `Data/ApplicationDbContext` (EF), `Data/QueryDbContext` (Dapper), `Factories/ChapterCheckerFactory`.
- `Application/Services/`: `JobRunnerService` (agendar e executar runs), `ChapterCheckingService` (checar e salvar capítulos). `Application/Scheduling/JobSchedule.cs`: cron em BRT (igual ao da API).
- `Domain/`: entidades (espelham as tabelas da API), `Services/BaseChapterChecker` (template method) e `MugiwaraOficialChecker` (o único site hoje), `Interfaces/IChapterChecker`, `Exceptions/ChapterCheckException`.
- **Sem migrations e sem criar tabelas**: o esquema é o SQL do `manga-whisper`. Entidade mudou → mudar lá e aqui.

## Funcionalidades
| Funcionalidade | Como acionar (comando, rota, tela → clique, atalho) | Código | Teste |
|----------------|------------------------------------------------------|--------|-------|
| Preparar o start (não recuperar o passado) | automático ao subir: runs `Running` viram falha, `Pending` são canceladas, jobs com próxima execução no passado pulam para a próxima | `Application/Services/JobRunnerService.cs` › `PrepareForStartupAsync` | — |
| Transformar jobs vencidos em runs | automático a cada 30 s: `ScrapingJobs` habilitados com `NextRunAt` no passado → `JobRuns` Pending, `NextRunAt` → próxima ocorrência do cron (BRT) | `JobRunnerService.ScheduleDueJobsAsync`; `Scheduling/JobSchedule.cs`; laço em `Infrastructure/Services/JobRunnerBackgroundService.cs` (`PollInterval`) | — |
| Executar runs pendentes, uma por vez | automático; no `manga-whisper`: painel → Checkers → **Run now**, ou um job agendado | `JobRunnerService.ExecuteNextPendingRunAsync` | — |
| Check next chapter (`CheckNext`) | run do tipo *Check next chapter*: procura `LastKnownChapter + 1` uma vez | `ChapterCheckingService.CheckNextChapterAsync`, `HasNewChapterAsync`, `ExtractNewChapterInfoAsync`, `SaveNewChapterAsync` | — |
| Backfill | run do tipo *Backfill*: salva capítulo por capítulo até faltar um | `ChapterCheckingService.ProcessAllAvailableChaptersForCheckerAsync` | — |
| Pular checker pausado | job agendado com checker `IsActive = false` é ignorado; Run now executa mesmo assim | `JobRunnerService` (agendamento) | — |
| Scraping do Mugiwaras Oficial (Selenium) | qualquer run de um checker desse site | `Domain/Services/MugiwaraOficialChecker.cs` (`BuildChapterUrl`, `CheckChapterExistsViaSeleniumRules`, `ExtractNewChapterInfoRulesAsync`, timeout de 20 s); base em `BaseChapterChecker.cs`; escolhido por `Infrastructure/Factories/ChapterCheckerFactory.cs` | — |
| Heartbeat | automático a cada 30 s: grava `WorkerHeartbeats.LastSeenAt`; o painel do `manga-whisper` mostra se o worker está vivo | `Infrastructure/Services/WorkerHeartbeatBackgroundService.cs` | — |

## Entradas e saídas
- Variáveis: `DefaultConnection` (PostgreSQL, o mesmo banco da API). No Docker: `CHROME_BINARY` e `CHROMEDRIVER_PATH` (definidas no `Dockerfile`); sem elas, o Selenium Manager acha o Chrome local e baixa o driver.
- Lê: `MangaCheckers`, `ScrapingJobs`, `JobRuns`, `Mangas`, `Chapters`. Grava: `JobRuns` (status, resultado, capítulos salvos), `Chapters`, `MangaCheckers` (status, último capítulo), `WorkerHeartbeats`.
- Rede: abre páginas de `https://mugiwarasoficial.org` com Chromium headless.

## Erros conhecidos e códigos de saída
- Chromium travando no Docker → falta memória compartilhada: o `docker-compose.yml` usa `shm_size: 1gb`.
- Worker parado na hora de um job → a execução é pulada, não refeita; pedidos feitos com ele parado são cancelados no próximo start.
- Site mudou o HTML → `ChapterCheckException` / run com falha: ajustar as regras do `MugiwaraOficialChecker`.
- Sem testes automatizados: a etapa `implement` do pipeline (TDD) precisa criar o primeiro projeto de teste (xUnit) na primeira fatia.

## Skills do projeto
Além das skills do pipeline. Buscas feitas com a `find-skills` (2026-10-20, junto com o `manga-whisper`): `xunit`, `dotnet test`, `selenium`, `docker compose`, `mediatr cqrs`, `dapper`.

| Skill | Fonte | Quando usar |
|-------|-------|-------------|
| `csharp-async` | `github/awesome-copilot` | async/await e cancelamento nos background services |
| `analyzing-dotnet-performance` | `dotnet/skills` | anti-padrões de performance .NET (oficial) |
| `optimizing-ef-core-queries` | `dotnet/skills` | queries EF Core dos repositórios e jobs (oficial) |
| `run-tests` | `dotnet/skills` | o comando `dotnet test` certo, filtros, cobertura (oficial) |
| `csharp-xunit` | `github/awesome-copilot` | escrever os testes xUnit (o projeto ainda não tem nenhum) |

Recusadas: `selenium-automation` (`mindrally/skills`), feita para Python/pytest e não para o Selenium em C# daqui; `dotnet-backend-patterns` (`wshobson/agents`), genérica e repete as acima.
