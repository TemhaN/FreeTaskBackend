# 🛠️ FreeTaskBackend

**FreeTaskBackend** — серверная часть платформы для фрилансеров и клиентов, построенная на **ASP.NET Core**.  
Обеспечивает полный набор фич: от аутентификации и заказов до real-time чатов, аналитики и Stripe-платежей.  
Контейнеризовано через Docker, с хранением данных в PostgreSQL и авторизацией через JWT.

## ✨ Возможности

- 🔐 **Аутентификация**: регистрация, вход, выход, управление профилем через JWT.
- 📋 **Заказы**: создание, принятие, оплата, завершение, подтверждение, отклонение.
- 💬 **Чаты**: real-time обмен сообщениями (SignalR), поддержка файлов.
- 📊 **Аналитика**: статистика по заказам, заработку, рейтингу (для фрилансеров).
- 🖼️ **Портфолио**: добавление, просмотр, удаление работ.
- 💳 **Платежи**: Stripe-интеграция для оплаты заказов.
- 🔔 **Уведомления**: моментальные уведомления через SignalR.
- 🔍 **Поиск**: фильтрация фрилансеров по навыкам, рейтингу, статусу.

## 📋 Требования

- [.NET 8 SDK](https://dotnet.microsoft.com/en-us/download)
- [PostgreSQL 14+](https://www.postgresql.org/download/)
- [Node.js 16+](https://nodejs.org/) *(если используется фронтенд)*
- [Docker](https://www.docker.com/) *(опционально)*
- Stripe API-ключи
- Современный браузер (для Swagger UI)

## 🧩 Зависимости

| Библиотека / Технология | Назначение |
|-------------------------|------------|
| `ASP.NET Core`          | Серверный фреймворк |
| `Entity Framework Core` | ORM для PostgreSQL |
| `SignalR`               | Real-time коммуникация |
| `Stripe`                | Онлайн-платежи |
| `JWT`                   | Аутентификация и авторизация |
| `Serilog`               | Логирование |
| `Swagger`               | Документация API |

Полный список — в `FreeTaskBackend.csproj`.

## 🚀 Установка и запуск

### 1. Клонируй проект
```bash
git clone https://github.com/YourUsername/FreeTaskBackend.git
cd FreeTaskBackend
````

### 2. Настрой `appsettings.json`

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=FreeTask;Username=postgres;Password=your_password"
  },
  "Jwt": {
    "Key": "your_jwt_secret_key",
    "Issuer": "your_issuer",
    "Audience": "your_audience"
  },
  "Stripe": {
    "SecretKey": "your_stripe_secret_key"
  }
}
```

> ⚠️ Обязательно замени `your_password`, `your_jwt_secret_key`, `your_stripe_secret_key` на свои значения.

### 3. Установи зависимости

```bash
dotnet restore
```

### 4. Применяй миграции

```bash
dotnet ef database update
```

### 5. Запускай

```bash
dotnet run
```

📍 Сервер поднимется на: [http://localhost:8080](http://localhost:8080)
📄 Swagger UI: [http://localhost:8080/swagger](http://localhost:8080/swagger)

## 🐳 Docker (опционально)

**Dockerfile**

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0
COPY bin/Release/net8.0/publish/ /app
WORKDIR /app
ENTRYPOINT ["dotnet", "FreeTaskBackend.dll"]
```

**docker-compose.yml**

```yaml
version: '3.8'
services:
  backend:
    build: .
    ports:
      - "8080:80"
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
    depends_on:
      - db

  db:
    image: postgres:14
    environment:
      - POSTGRES_USER=postgres
      - POSTGRES_PASSWORD=your_password
      - POSTGRES_DB=FreeTask
    volumes:
      - postgres_data:/var/lib/postgresql/data

volumes:
  postgres_data:
```

🚀 Запуск:

```bash
docker-compose up --build
```

## 🖱️ Использование

Запускай сервер:

```bash
dotnet run
```

### 🔧 Основные эндпоинты (подробнее в Swagger):

| Функция     | Эндпоинт                                     |
| ----------- | -------------------------------------------- |
| Регистрация | `POST /api/auth/register`                    |
| Вход        | `POST /api/auth/login`                       |
| Заказы      | `GET/POST /api/orders`, `/api/orders/{id}`   |
| Чат         | `GET /api/chats`, `POST /api/chats/messages` |
| Портфолио   | `GET/POST /api/portfolio`, `/portfolio/{id}` |
| Аналитика   | `GET /api/analytics/{userId}`                |
| Платежи     | `POST /api/payments/create-checkout-session` |

### 🔗 SignalR

* Хаб: `/chatHub`
* Авторизация: JWT в query string или headers

### 👮‍♂️ Админ-доступ

Некоторые эндпоинты требуют роли `Admin`.

## 📦 Сборка и деплой

Релизная сборка:

```bash
dotnet publish -c Release
```

📁 Файлы появятся в `bin/Release/net8.0/publish/`

🚀 Развёртывание:

* Скопируй на сервер.
* Убедись, что PostgreSQL и Stripe доступны.
* Проверь настройки CORS (если фронт и бэк на разных доменах).

## 📸 Скриншоты

<div style="display: flex; flex-wrap: wrap; gap: 10px; justify-content: center;">
  <img src="https://github.com/TemhaN/FreeTaskBackend/blob/master/FreeTaskBackend/Screenshots/1.png?raw=true" alt="FreeTaskBackend" width="30%">
  <img src="https://github.com/TemhaN/FreeTaskBackend/blob/master/FreeTaskBackend/Screenshots/2.png?raw=true" alt="FreeTaskBackend" width="30%">
  <img src="https://github.com/TemhaN/FreeTaskBackend/blob/master/FreeTaskBackend/Screenshots/3.png?raw=true" alt="FreeTaskBackend" width="30%">
</div>    

## 🧠 Автор

**TemhaN**  
[GitHub профиль](https://github.com/TemhaN)

## 🧾 Лицензия

Проект распространяется под лицензией [MIT License].

## 📬 Обратная связь

Нашли баг или хотите предложить улучшение?
Создайте **issue** или присылайте **pull request** в репозиторий!

## ⚙️ Технологии

* **ASP.NET Core** — API-сервер
* **EF Core** — ORM для PostgreSQL
* **SignalR** — WebSocket-подключение
* **Stripe** — платёжный шлюз
* **JWT** — авторизация
* **Serilog** — логирование
* **Swagger** — интерактивная документация
* **Docker** — упаковка в контейнеры
