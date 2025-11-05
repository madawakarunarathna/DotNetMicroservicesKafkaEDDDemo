# .NET Microservices Event Driven Application
A lightweight e-commerce demo showcasing .NET 8 microservices communicating via Kafka events, using Entity Framework Core (in-memory) and Docker Compose for full container orchestration.

# Scope
This solution implements two independent services:
- User Service – manages user registration and information.
- Order Service – handles order creation and management.

Both services:
- Expose RESTful APIs
- Persist data in EF Core in-memory stores
- Publish and consume Kafka events (UserCreated, OrderCreated)
- Are containerized and orchestrated via a single docker-compose.yml

# Architecture Overview
Key Components
- User Service → Publishes UserCreated events → Consumed by Order Service
- Order Service → Publishes OrderCreated events → Consumed by User Service
- Kafka (Redpanda) → Acts as message broker
- Docker Compose → Spins up all components in one network

UserService ---> Kafka (topic: user-created) ---> OrderService

OrderService ---> Kafka (topic: order-created) ---> UserService

# Services
## User Service
- POST /users – create a new user
- GET /users/{id} – retrieve user by ID
- GET /users – list all users

Responsibilities
- Validate and register users
- Ensure email uniqueness
- Publish UserCreated event to Kafka

Swagger UI: http://localhost:8080/swagger

## Order Service
- Endpoints
- POST /orders – create a new order
- GET /orders/{id} – retrieve order by ID
- GET /orders – list all orders

Responsibilities
- Create and manage orders
- Validate referenced user
- Publish OrderCreated event to Kafka

  Swagger UI: http://localhost:8081/swagger

# Eventing
| Event          | Publisher     | Consumer      | Topic Name      | Description                         |
| -------------- | ------------- | ------------- | --------------- | ----------------------------------- |
| `UserCreated`  | User Service  | Order Service | `user-created`  | Triggers when a new user is added   |
| `OrderCreated` | Order Service | User Service  | `order-created` | Triggers when a new order is placed |

### Consumer Groups
- user-service-group
- order-service-group

# Running Locally
## Prerequisites
- .NET 8 SDK
- Docker Desktop
- Git

### Commands
`git clone <repo-url>`
`cd DotNetMicroservicesKafkaEDDDemo/deploy/`
`docker compose build`
`docker compose up -d`

### Access
- User Service → http://localhost:8080/swagger
- Order Service → http://localhost:8081/swagger
- Kafka Console (Redpanda) → http://localhost:8080

### Stop containers:
`docker-compose down`

# Testing
Unit tests are written using xUnit, Moq, and FluentAssertions to validate:
- Service logic (user creation, order creation)
- Validation behavior
- Event publication workflow
### Run tests locally:
`dotnet test`

# Configuration
| Setting                  | Description            | Example              |
| ------------------------ | ---------------------- | -------------------- |
| `Kafka:BootstrapServers` | Kafka broker endpoint  | `kafka:9092`         |
| `Kafka:Topics:Users`     | Topic for user events  | `user-created`       |
| `Kafka:Topics:Orders`    | Topic for order events | `order-created`      |
| `Kafka:ConsumerGroup`    | Group ID for consumer  | `user-service-group` |

Each service reads from its own `appsettings.json`

# Assumptions & Decisions
- Kafka topics auto-created via Redpanda (auto_create_topics_enabled=true)
- No persistent storage required per spec → in-memory EF Core used
- Test project runs locally, not containerized
- Minimal validation to demonstrate event flow, not production hardening

# Troubleshooting
| Issue               | Cause                    | Fix                                  |
| ------------------- | ------------------------ | ------------------------------------ |
| Kafka not reachable | Containers not networked | Ensure `appnet` network in compose   |
| Event not consumed  | Wrong topic or group     | Verify topic names and group IDs     |
| Swagger unavailable | Port conflict            | Adjust exposed ports in compose file |

# AI Tool Usage
This project was partially assisted by ChatGPT (GPT-5) for:
- Architecture planning
- Docker and Kafka configuration guidance
- Code structure recommendations
- Documentation templates

All implementation decisions were reviewed and adjusted manually.
