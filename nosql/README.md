# Лабораторна 2: NoSQL

Контент курсу (модулі, уроки, матеріали, тести) винесено в MongoDB і порівняно з тією ж частиною в PostgreSQL.
Redis — приклади базових команд.

## Запуск

```bash
cd nosql
docker compose up -d
cd LmsNoSql
dotnet run -- 1000     # заповнити бази 1000 курсів і запустити тести
dotnet run -- redis    # команди Redis
```

Результати запусків — у папці `results/`.
