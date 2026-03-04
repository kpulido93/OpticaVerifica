#!/bin/bash
# Optima Verifica - Development Setup Script

set -e

echo "🚀 Starting Optima Verifica Development Environment..."

# Check if .env exists
if [ ! -f .env ]; then
    echo "📝 Creating .env from .env.example..."
    cp .env.example .env
fi

# Start Docker services
echo "🐳 Starting Docker containers..."
docker-compose up -d mysql

# Wait for MySQL to be ready
echo "⏳ Waiting for MySQL to be ready..."
sleep 10

# Run migrations
echo "📦 Running database migrations..."
docker-compose exec -T mysql mysql -u root -p${MYSQL_ROOT_PASSWORD:-root} ${MYSQL_DATABASE:-neon_templaris} < ./db/migrations/001_initial_schema.sql

# Run seeds
echo "🌱 Running database seeds..."
docker-compose exec -T mysql mysql -u root -p${MYSQL_ROOT_PASSWORD:-root} ${MYSQL_DATABASE:-neon_templaris} < ./db/seeds/001_initial_data.sql

# Start API and Worker
echo "🔧 Starting API and Worker..."
docker-compose up -d api worker

# Start Frontend
echo "🎨 Starting Frontend..."
docker-compose up -d frontend

echo ""
echo "✅ Optima Verifica is ready!"
echo ""
echo "📍 Access points:"
echo "   Frontend:  http://localhost:3000"
echo "   API:       http://localhost:5000"
echo "   API Docs:  http://localhost:5000/swagger"
echo ""
echo "🔑 Demo credentials:"
echo "   Admin:    admin / admin123"
echo "   Operator: operator / operator123"
echo "   Reader:   reader / reader123"
