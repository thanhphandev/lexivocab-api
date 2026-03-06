# 🏗️ Terraform Infrastructure-as-Code Guide

> Provisioning toàn bộ hạ tầng cloud cho LexiVocab API bằng Terraform.

---

## 1. Tổng quan

### Tại sao dùng Terraform?

| Vấn đề | Giải pháp Terraform |
|--------|---------------------|
| Click tay trên console → quên bước, không reproduce | Code hóa toàn bộ infra → `terraform apply` |
| Dev/Staging/Prod khác nhau | Dùng `terraform.tfvars` riêng per environment |
| Không biết infra đang ở state nào | `terraform plan` hiển thị diff trước khi apply |
| Xóa tài nguyên quên → bill phát sinh | `terraform destroy` xóa sạch toàn bộ |
| Team mới onboard không biết infra gì | Đọc `.tf` files = hiểu hết infra |

### Infra cần provisioning cho LexiVocab

```
┌─────────────────────────────────────────────────┐
│                   VPC / Network                  │
│                                                   │
│  ┌──────────┐  ┌──────────┐  ┌───────────────┐  │
│  │ App Service│  │PostgreSQL│  │    Redis       │  │
│  │ / ECS     │  │ Managed  │  │    Managed     │  │
│  │           │  │          │  │               │  │
│  │ Port 8080 │  │ Port 5432│  │  Port 6379    │  │
│  └─────┬─────┘  └──────────┘  └───────────────┘  │
│        │                                          │
│  ┌─────┴──────────┐  ┌────────────────────────┐  │
│  │ Load Balancer   │  │   Container Registry   │  │
│  │ (ALB / AppGW)   │  │   (ECR / ACR)          │  │
│  │ HTTPS :443      │  │                        │  │
│  └────────────────┘  └────────────────────────┘  │
│                                                   │
│  ┌────────────────────────────────────────────┐  │
│  │         Monitoring & Logging                │  │
│  │   CloudWatch / App Insights / SEQ           │  │
│  └────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────┘
```

---

## 2. Project Structure

```
infra/
├── modules/                     # Reusable modules
│   ├── networking/
│   │   ├── main.tf
│   │   ├── variables.tf
│   │   └── outputs.tf
│   ├── database/
│   │   ├── main.tf
│   │   ├── variables.tf
│   │   └── outputs.tf
│   ├── cache/
│   │   ├── main.tf
│   │   ├── variables.tf
│   │   └── outputs.tf
│   └── app/
│       ├── main.tf
│       ├── variables.tf
│       └── outputs.tf
│
├── environments/
│   ├── dev/
│   │   ├── main.tf              # Compose modules
│   │   ├── variables.tf
│   │   ├── terraform.tfvars     # Dev-specific values
│   │   ├── backend.tf           # Remote state config
│   │   └── outputs.tf
│   ├── staging/
│   │   └── ...
│   └── prod/
│       └── ...
│
├── .gitignore                   # *.tfstate, *.tfvars (secrets)
└── README.md
```

---

## 3. AWS Implementation

### 3.1 Backend State (S3 + DynamoDB)

```hcl
# infra/environments/prod/backend.tf

terraform {
  required_version = ">= 1.5"

  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 5.0"
    }
  }

  # Remote state — cho phép team collaboration
  backend "s3" {
    bucket         = "lexivocab-terraform-state"
    key            = "prod/terraform.tfstate"
    region         = "ap-southeast-1"
    dynamodb_table = "terraform-locks"          # State locking (tránh race condition)
    encrypt        = true
  }
}

provider "aws" {
  region = var.aws_region

  default_tags {
    tags = {
      Project     = "LexiVocab"
      Environment = var.environment
      ManagedBy   = "Terraform"
    }
  }
}
```

### 3.2 Variables

```hcl
# infra/environments/prod/variables.tf

variable "aws_region" {
  description = "AWS region"
  type        = string
  default     = "ap-southeast-1"
}

variable "environment" {
  description = "Environment name"
  type        = string
  default     = "prod"
}

variable "app_name" {
  description = "Application name"
  type        = string
  default     = "lexivocab"
}

# ─── Database ────────────────────────────────────
variable "db_instance_class" {
  description = "RDS instance type"
  type        = string
  default     = "db.t4g.micro"       # Free-tier eligible
}

variable "db_name" {
  type    = string
  default = "lexivocab"
}

variable "db_username" {
  type      = string
  sensitive = true
}

variable "db_password" {
  type      = string
  sensitive = true
}

# ─── Cache ───────────────────────────────────────
variable "redis_node_type" {
  type    = string
  default = "cache.t4g.micro"
}

# ─── App ─────────────────────────────────────────
variable "app_cpu" {
  type    = number
  default = 256           # 0.25 vCPU
}

variable "app_memory" {
  type    = number
  default = 512           # 512 MB
}

variable "app_desired_count" {
  type    = number
  default = 1             # Scale khi cần
}

variable "jwt_secret" {
  type      = string
  sensitive = true
}

variable "docker_image" {
  type    = string
  default = "lexivocab-api:latest"
}
```

### 3.3 Networking Module

```hcl
# infra/modules/networking/main.tf

resource "aws_vpc" "main" {
  cidr_block           = "10.0.0.0/16"
  enable_dns_hostnames = true
  enable_dns_support   = true

  tags = { Name = "${var.app_name}-${var.environment}-vpc" }
}

# ─── Public Subnets (ALB) ────────────────────────
resource "aws_subnet" "public" {
  count                   = 2
  vpc_id                  = aws_vpc.main.id
  cidr_block              = "10.0.${count.index + 1}.0/24"
  availability_zone       = data.aws_availability_zones.available.names[count.index]
  map_public_ip_on_launch = true

  tags = { Name = "${var.app_name}-public-${count.index + 1}" }
}

# ─── Private Subnets (App + DB + Cache) ──────────
resource "aws_subnet" "private" {
  count             = 2
  vpc_id            = aws_vpc.main.id
  cidr_block        = "10.0.${count.index + 10}.0/24"
  availability_zone = data.aws_availability_zones.available.names[count.index]

  tags = { Name = "${var.app_name}-private-${count.index + 1}" }
}

# ─── Internet Gateway ───────────────────────────
resource "aws_internet_gateway" "main" {
  vpc_id = aws_vpc.main.id
}

# ─── NAT Gateway (cho private subnets truy cập internet) ─
resource "aws_eip" "nat" {
  domain = "vpc"
}

resource "aws_nat_gateway" "main" {
  allocation_id = aws_eip.nat.id
  subnet_id     = aws_subnet.public[0].id
}

# ─── Route Tables ───────────────────────────────
resource "aws_route_table" "public" {
  vpc_id = aws_vpc.main.id

  route {
    cidr_block = "0.0.0.0/0"
    gateway_id = aws_internet_gateway.main.id
  }
}

resource "aws_route_table" "private" {
  vpc_id = aws_vpc.main.id

  route {
    cidr_block     = "0.0.0.0/0"
    nat_gateway_id = aws_nat_gateway.main.id
  }
}

resource "aws_route_table_association" "public" {
  count          = 2
  subnet_id      = aws_subnet.public[count.index].id
  route_table_id = aws_route_table.public.id
}

resource "aws_route_table_association" "private" {
  count          = 2
  subnet_id      = aws_subnet.private[count.index].id
  route_table_id = aws_route_table.private.id
}

# ─── Data Sources ───────────────────────────────
data "aws_availability_zones" "available" {
  state = "available"
}

# ─── Outputs ────────────────────────────────────
output "vpc_id" { value = aws_vpc.main.id }
output "public_subnet_ids" { value = aws_subnet.public[*].id }
output "private_subnet_ids" { value = aws_subnet.private[*].id }
```

### 3.4 Database Module (RDS PostgreSQL)

```hcl
# infra/modules/database/main.tf

resource "aws_db_subnet_group" "main" {
  name       = "${var.app_name}-${var.environment}-db-subnet"
  subnet_ids = var.private_subnet_ids

  tags = { Name = "${var.app_name} DB Subnet Group" }
}

resource "aws_security_group" "db" {
  name_prefix = "${var.app_name}-db-"
  vpc_id      = var.vpc_id

  # Chỉ cho phép app truy cập
  ingress {
    from_port       = 5432
    to_port         = 5432
    protocol        = "tcp"
    security_groups = [var.app_security_group_id]
  }

  egress {
    from_port   = 0
    to_port     = 0
    protocol    = "-1"
    cidr_blocks = ["0.0.0.0/0"]
  }
}

resource "aws_db_instance" "postgres" {
  identifier     = "${var.app_name}-${var.environment}"
  engine         = "postgres"
  engine_version = "16.4"
  instance_class = var.db_instance_class

  db_name  = var.db_name
  username = var.db_username
  password = var.db_password

  allocated_storage     = 20
  max_allocated_storage = 100        # Auto-scaling storage

  # Network
  db_subnet_group_name   = aws_db_subnet_group.main.name
  vpc_security_group_ids = [aws_security_group.db.id]
  publicly_accessible    = false     # ← QUAN TRỌNG: private only

  # Backup
  backup_retention_period = 7        # 7 ngày backup
  backup_window           = "03:00-04:00"

  # Maintenance
  maintenance_window          = "Mon:04:00-Mon:05:00"
  auto_minor_version_upgrade = true

  # Performance Insights (free tier)
  performance_insights_enabled = true

  # Deletion protection (production)
  deletion_protection = var.environment == "prod"

  # Skip final snapshot khi dev
  skip_final_snapshot = var.environment != "prod"
  final_snapshot_identifier = var.environment == "prod" ? "${var.app_name}-final-snapshot" : null

  tags = { Name = "${var.app_name}-postgres" }
}

output "db_endpoint" { value = aws_db_instance.postgres.endpoint }
output "db_connection_string" {
  value     = "Host=${aws_db_instance.postgres.address};Port=5432;Database=${var.db_name};Username=${var.db_username};Password=${var.db_password}"
  sensitive = true
}
```

### 3.5 Cache Module (ElastiCache Redis)

```hcl
# infra/modules/cache/main.tf

resource "aws_elasticache_subnet_group" "main" {
  name       = "${var.app_name}-cache-subnet"
  subnet_ids = var.private_subnet_ids
}

resource "aws_security_group" "cache" {
  name_prefix = "${var.app_name}-cache-"
  vpc_id      = var.vpc_id

  ingress {
    from_port       = 6379
    to_port         = 6379
    protocol        = "tcp"
    security_groups = [var.app_security_group_id]
  }

  egress {
    from_port   = 0
    to_port     = 0
    protocol    = "-1"
    cidr_blocks = ["0.0.0.0/0"]
  }
}

resource "aws_elasticache_cluster" "redis" {
  cluster_id           = "${var.app_name}-${var.environment}"
  engine               = "redis"
  engine_version       = "7.1"
  node_type            = var.redis_node_type
  num_cache_nodes      = 1
  parameter_group_name = "default.redis7"

  subnet_group_name  = aws_elasticache_subnet_group.main.name
  security_group_ids = [aws_security_group.cache.id]

  # Maintenance
  maintenance_window = "Sun:05:00-Sun:06:00"

  tags = { Name = "${var.app_name}-redis" }
}

output "redis_endpoint" {
  value = aws_elasticache_cluster.redis.cache_nodes[0].address
}
output "redis_connection_string" {
  value = "${aws_elasticache_cluster.redis.cache_nodes[0].address}:6379"
}
```

### 3.6 App Module (ECS Fargate)

```hcl
# infra/modules/app/main.tf

# ─── ECR Repository ─────────────────────────────
resource "aws_ecr_repository" "api" {
  name                 = "${var.app_name}-api"
  image_tag_mutability = "MUTABLE"

  image_scanning_configuration {
    scan_on_push = true
  }

  lifecycle {
    prevent_destroy = true
  }
}

# ─── ECS Cluster ────────────────────────────────
resource "aws_ecs_cluster" "main" {
  name = "${var.app_name}-${var.environment}"

  setting {
    name  = "containerInsights"
    value = "enabled"
  }
}

# ─── IAM Role for ECS Tasks ────────────────────
resource "aws_iam_role" "ecs_task_execution" {
  name = "${var.app_name}-ecs-task-execution"

  assume_role_policy = jsonencode({
    Version = "2012-10-17"
    Statement = [{
      Action    = "sts:AssumeRole"
      Effect    = "Allow"
      Principal = { Service = "ecs-tasks.amazonaws.com" }
    }]
  })
}

resource "aws_iam_role_policy_attachment" "ecs_task_execution" {
  role       = aws_iam_role.ecs_task_execution.name
  policy_arn = "arn:aws:iam::aws:policy/service-role/AmazonECSTaskExecutionRolePolicy"
}

# ─── CloudWatch Log Group ──────────────────────
resource "aws_cloudwatch_log_group" "api" {
  name              = "/ecs/${var.app_name}-${var.environment}"
  retention_in_days = 30
}

# ─── Security Group (App) ──────────────────────
resource "aws_security_group" "app" {
  name_prefix = "${var.app_name}-app-"
  vpc_id      = var.vpc_id

  ingress {
    from_port       = 8080
    to_port         = 8080
    protocol        = "tcp"
    security_groups = [aws_security_group.alb.id]
  }

  egress {
    from_port   = 0
    to_port     = 0
    protocol    = "-1"
    cidr_blocks = ["0.0.0.0/0"]
  }
}

# ─── ECS Task Definition ──────────────────────
resource "aws_ecs_task_definition" "api" {
  family                   = "${var.app_name}-api"
  network_mode             = "awsvpc"
  requires_compatibilities = ["FARGATE"]
  cpu                      = var.app_cpu
  memory                   = var.app_memory
  execution_role_arn       = aws_iam_role.ecs_task_execution.arn

  container_definitions = jsonencode([{
    name  = "api"
    image = "${aws_ecr_repository.api.repository_url}:latest"

    portMappings = [{
      containerPort = 8080
      protocol      = "tcp"
    }]

    environment = [
      { name = "ASPNETCORE_ENVIRONMENT", value = var.environment == "prod" ? "Production" : "Development" },
      { name = "ASPNETCORE_URLS",        value = "http://+:8080" },
      { name = "App__Url",               value = var.app_url }
    ]

    secrets = [
      { name = "ConnectionStrings__DefaultConnection", valueFrom = aws_ssm_parameter.db_connection.arn },
      { name = "ConnectionStrings__Redis",             valueFrom = aws_ssm_parameter.redis_connection.arn },
      { name = "Jwt__Secret",                          valueFrom = aws_ssm_parameter.jwt_secret.arn }
    ]

    logConfiguration = {
      logDriver = "awslogs"
      options = {
        "awslogs-group"         = aws_cloudwatch_log_group.api.name
        "awslogs-region"        = var.aws_region
        "awslogs-stream-prefix" = "api"
      }
    }

    healthCheck = {
      command     = ["CMD-SHELL", "curl -f http://localhost:8080/healthz || exit 1"]
      interval    = 30
      timeout     = 5
      retries     = 3
      startPeriod = 60
    }
  }])
}

# ─── ECS Service ───────────────────────────────
resource "aws_ecs_service" "api" {
  name            = "${var.app_name}-api"
  cluster         = aws_ecs_cluster.main.id
  task_definition = aws_ecs_task_definition.api.arn
  desired_count   = var.app_desired_count
  launch_type     = "FARGATE"

  network_configuration {
    subnets          = var.private_subnet_ids
    security_groups  = [aws_security_group.app.id]
    assign_public_ip = false
  }

  load_balancer {
    target_group_arn = aws_lb_target_group.api.arn
    container_name   = "api"
    container_port   = 8080
  }

  depends_on = [aws_lb_listener.https]
}

# ─── ALB (Application Load Balancer) ───────────
resource "aws_security_group" "alb" {
  name_prefix = "${var.app_name}-alb-"
  vpc_id      = var.vpc_id

  ingress {
    from_port   = 443
    to_port     = 443
    protocol    = "tcp"
    cidr_blocks = ["0.0.0.0/0"]
  }

  ingress {
    from_port   = 80
    to_port     = 80
    protocol    = "tcp"
    cidr_blocks = ["0.0.0.0/0"]
  }

  egress {
    from_port   = 0
    to_port     = 0
    protocol    = "-1"
    cidr_blocks = ["0.0.0.0/0"]
  }
}

resource "aws_lb" "main" {
  name               = "${var.app_name}-${var.environment}"
  internal           = false
  load_balancer_type = "application"
  security_groups    = [aws_security_group.alb.id]
  subnets            = var.public_subnet_ids
}

resource "aws_lb_target_group" "api" {
  name        = "${var.app_name}-api"
  port        = 8080
  protocol    = "HTTP"
  vpc_id      = var.vpc_id
  target_type = "ip"

  health_check {
    enabled             = true
    path                = "/healthz"
    healthy_threshold   = 2
    unhealthy_threshold = 3
    interval            = 30
    timeout             = 5
  }
}

resource "aws_lb_listener" "https" {
  load_balancer_arn = aws_lb.main.arn
  port              = 443
  protocol          = "HTTPS"
  ssl_policy        = "ELBSecurityPolicy-TLS13-1-2-2021-06"
  certificate_arn   = var.ssl_certificate_arn

  default_action {
    type             = "forward"
    target_group_arn = aws_lb_target_group.api.arn
  }
}

# HTTP → HTTPS redirect
resource "aws_lb_listener" "http_redirect" {
  load_balancer_arn = aws_lb.main.arn
  port              = 80
  protocol          = "HTTP"

  default_action {
    type = "redirect"
    redirect {
      port        = "443"
      protocol    = "HTTPS"
      status_code = "HTTP_301"
    }
  }
}

# ─── SSM Parameters (Secrets) ──────────────────
resource "aws_ssm_parameter" "db_connection" {
  name  = "/${var.app_name}/${var.environment}/db-connection"
  type  = "SecureString"
  value = var.db_connection_string
}

resource "aws_ssm_parameter" "redis_connection" {
  name  = "/${var.app_name}/${var.environment}/redis-connection"
  type  = "SecureString"
  value = var.redis_connection_string
}

resource "aws_ssm_parameter" "jwt_secret" {
  name  = "/${var.app_name}/${var.environment}/jwt-secret"
  type  = "SecureString"
  value = var.jwt_secret
}

# ─── Outputs ───────────────────────────────────
output "alb_dns_name" { value = aws_lb.main.dns_name }
output "ecr_repository_url" { value = aws_ecr_repository.api.repository_url }
output "app_security_group_id" { value = aws_security_group.app.id }
```

### 3.7 Main Environment Config (compose modules)

```hcl
# infra/environments/prod/main.tf

module "networking" {
  source      = "../../modules/networking"
  app_name    = var.app_name
  environment = var.environment
}

module "app" {
  source             = "../../modules/app"
  app_name           = var.app_name
  environment        = var.environment
  aws_region         = var.aws_region
  vpc_id             = module.networking.vpc_id
  public_subnet_ids  = module.networking.public_subnet_ids
  private_subnet_ids = module.networking.private_subnet_ids
  app_cpu            = var.app_cpu
  app_memory         = var.app_memory
  app_desired_count  = var.app_desired_count
  app_url            = "https://api.lexivocab.store"
  ssl_certificate_arn = var.ssl_certificate_arn
  jwt_secret          = var.jwt_secret
  db_connection_string    = module.database.db_connection_string
  redis_connection_string = module.cache.redis_connection_string
  docker_image       = var.docker_image
}

module "database" {
  source                = "../../modules/database"
  app_name              = var.app_name
  environment           = var.environment
  vpc_id                = module.networking.vpc_id
  private_subnet_ids    = module.networking.private_subnet_ids
  app_security_group_id = module.app.app_security_group_id
  db_instance_class     = var.db_instance_class
  db_name               = var.db_name
  db_username           = var.db_username
  db_password           = var.db_password
}

module "cache" {
  source                = "../../modules/cache"
  app_name              = var.app_name
  environment           = var.environment
  vpc_id                = module.networking.vpc_id
  private_subnet_ids    = module.networking.private_subnet_ids
  app_security_group_id = module.app.app_security_group_id
  redis_node_type       = var.redis_node_type
}

# ─── Outputs ───────────────────────────────────
output "api_url" {
  value = "https://${module.app.alb_dns_name}"
}

output "ecr_repository" {
  value = module.app.ecr_repository_url
}
```

### 3.8 tfvars per environment

```hcl
# infra/environments/prod/terraform.tfvars
# ⚠️ FILE NÀY PHẢI NẰM TRONG .gitignore

environment       = "prod"
aws_region        = "ap-southeast-1"
app_name          = "lexivocab"

db_instance_class = "db.t4g.small"
db_name           = "lexivocab"
db_username       = "lexivocab_app"
db_password       = "your-super-secret-password-here"   # hoặc dùng AWS Secrets Manager

redis_node_type   = "cache.t4g.micro"

app_cpu           = 512
app_memory        = 1024
app_desired_count = 2        # HA: chạy 2 instances

jwt_secret        = "YourProductionJwtSecretAtLeast32Characters!"
ssl_certificate_arn = "arn:aws:acm:ap-southeast-1:123456789:certificate/xxx"
```

```hcl
# infra/environments/dev/terraform.tfvars

environment       = "dev"
aws_region        = "ap-southeast-1"
app_name          = "lexivocab"

db_instance_class = "db.t4g.micro"        # Nhỏ nhất, free-tier
db_name           = "lexivocab_dev"
db_username       = "dev_user"
db_password       = "dev-password-123"

redis_node_type   = "cache.t4g.micro"

app_cpu           = 256
app_memory        = 512
app_desired_count = 1                      # 1 instance cho dev
```

---

## 4. CI/CD Tích hợp Terraform

### GitHub Actions — Full Pipeline

```yaml
# .github/workflows/deploy.yml
name: Build, Test & Deploy

on:
  push:
    branches: [main]
  pull_request:
    branches: [main]

env:
  AWS_REGION: ap-southeast-1

jobs:
  # ═══ Job 1: Test Application ═══════════════════
  test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'
      - run: dotnet restore
      - run: dotnet build --no-restore
      - run: dotnet test --no-build --verbosity normal

  # ═══ Job 2: Build & Push Docker Image ═════════
  build:
    needs: test
    if: github.ref == 'refs/heads/main'
    runs-on: ubuntu-latest
    outputs:
      image_tag: ${{ steps.meta.outputs.tags }}
    steps:
      - uses: actions/checkout@v4

      - name: Configure AWS credentials
        uses: aws-actions/configure-aws-credentials@v4
        with:
          aws-access-key-id: ${{ secrets.AWS_ACCESS_KEY_ID }}
          aws-secret-access-key: ${{ secrets.AWS_SECRET_ACCESS_KEY }}
          aws-region: ${{ env.AWS_REGION }}

      - name: Login to ECR
        id: ecr-login
        uses: aws-actions/amazon-ecr-login@v2

      - name: Build and push
        id: meta
        env:
          ECR_REGISTRY: ${{ steps.ecr-login.outputs.registry }}
          IMAGE_TAG: ${{ github.sha }}
        run: |
          docker build -t $ECR_REGISTRY/lexivocab-api:$IMAGE_TAG \
                        -t $ECR_REGISTRY/lexivocab-api:latest .
          docker push $ECR_REGISTRY/lexivocab-api:$IMAGE_TAG
          docker push $ECR_REGISTRY/lexivocab-api:latest
          echo "tags=$ECR_REGISTRY/lexivocab-api:$IMAGE_TAG" >> $GITHUB_OUTPUT

  # ═══ Job 3: Terraform Plan (on PR) ════════════
  terraform-plan:
    if: github.event_name == 'pull_request'
    needs: test
    runs-on: ubuntu-latest
    defaults:
      run:
        working-directory: infra/environments/prod
    steps:
      - uses: actions/checkout@v4

      - name: Setup Terraform
        uses: hashicorp/setup-terraform@v3
        with:
          terraform_version: "1.9.0"

      - name: Configure AWS credentials
        uses: aws-actions/configure-aws-credentials@v4
        with:
          aws-access-key-id: ${{ secrets.AWS_ACCESS_KEY_ID }}
          aws-secret-access-key: ${{ secrets.AWS_SECRET_ACCESS_KEY }}
          aws-region: ${{ env.AWS_REGION }}

      - name: Terraform Init
        run: terraform init

      - name: Terraform Plan
        run: terraform plan -no-color -var-file=terraform.tfvars
        env:
          TF_VAR_db_password: ${{ secrets.DB_PASSWORD }}
          TF_VAR_jwt_secret: ${{ secrets.JWT_SECRET }}

      # Comment plan output trên PR
      - name: Comment PR
        uses: actions/github-script@v7
        if: github.event_name == 'pull_request'
        with:
          script: |
            github.rest.issues.createComment({
              issue_number: context.issue.number,
              owner: context.repo.owner,
              repo: context.repo.repo,
              body: '## Terraform Plan\n```\n${{ steps.plan.outputs.stdout }}\n```'
            })

  # ═══ Job 4: Terraform Apply + Deploy (on main) ═
  deploy:
    needs: build
    if: github.ref == 'refs/heads/main'
    runs-on: ubuntu-latest
    defaults:
      run:
        working-directory: infra/environments/prod
    steps:
      - uses: actions/checkout@v4

      - uses: hashicorp/setup-terraform@v3
        with:
          terraform_version: "1.9.0"

      - name: Configure AWS credentials
        uses: aws-actions/configure-aws-credentials@v4
        with:
          aws-access-key-id: ${{ secrets.AWS_ACCESS_KEY_ID }}
          aws-secret-access-key: ${{ secrets.AWS_SECRET_ACCESS_KEY }}
          aws-region: ${{ env.AWS_REGION }}

      - name: Terraform Init & Apply
        run: |
          terraform init
          terraform apply -auto-approve -var-file=terraform.tfvars
        env:
          TF_VAR_db_password: ${{ secrets.DB_PASSWORD }}
          TF_VAR_jwt_secret: ${{ secrets.JWT_SECRET }}

      - name: Update ECS Service (force new deployment)
        run: |
          aws ecs update-service \
            --cluster lexivocab-prod \
            --service lexivocab-api \
            --force-new-deployment
```

---

## 5. Terraform Commands Cheat Sheet

```bash
# ─── Khởi tạo (chạy 1 lần đầu) ─────────────────
terraform init

# ─── Xem trước thay đổi (DRY RUN) ──────────────
terraform plan -var-file=terraform.tfvars

# ─── Áp dụng thay đổi ──────────────────────────
terraform apply -var-file=terraform.tfvars

# ─── Xóa toàn bộ infra (NGUY HIỂM) ────────────
terraform destroy -var-file=terraform.tfvars

# ─── Xem state hiện tại ────────────────────────
terraform state list
terraform state show module.database.aws_db_instance.postgres

# ─── Import resource có sẵn vào state ──────────
terraform import module.database.aws_db_instance.postgres lexivocab-prod

# ─── Format code ───────────────────────────────
terraform fmt -recursive

# ─── Validate cú pháp ─────────────────────────
terraform validate

# ─── Refresh state (sync với thực tế) ──────────
terraform refresh

# ─── Lock/Unlock state (khi bị stuck) ──────────
terraform force-unlock <lock-id>
```

---

## 6. Security Best Practices

```
☐ KHÔNG commit terraform.tfvars chứa secrets → .gitignore
☐ Dùng remote state (S3/Azure Blob) + state locking
☐ Sensitive vars: variable "x" { sensitive = true }
☐ Secrets qua CI/CD environment variables (TF_VAR_xxx)
☐ Database nằm private subnet, KHÔNG public
☐ Security groups: least privilege (chỉ mở port cần thiết)
☐ RDS: deletion_protection = true cho production
☐ Enable encryption at rest (S3, RDS, ElastiCache)
☐ IAM roles/policies: least privilege principle
☐ Terraform version pinned: required_version = ">= 1.5"
☐ Provider version pinned: version = "~> 5.0"
☐ Enable state file encryption
```

---

## 7. Ước tính chi phí AWS (tham khảo)

| Resource | Spec | Giá/tháng (ước tính) |
|----------|------|---------------------|
| ECS Fargate (1 task) | 0.25 vCPU + 512MB | ~$10 |
| RDS PostgreSQL | db.t4g.micro | ~$13 (free-tier eligible) |
| ElastiCache Redis | cache.t4g.micro | ~$12 |
| ALB | Basic | ~$16 |
| NAT Gateway | 1 AZ | ~$32 |
| ECR | 1GB images | ~$0.10 |
| CloudWatch | Basic | ~$0 |
| **Tổng (minimal)** | | **~$83/tháng** |

> **Tip tiết kiệm**: Dev environment dùng `t4g.micro` + 1 AZ + no NAT Gateway (public subnet) giảm còn ~$35/tháng.
