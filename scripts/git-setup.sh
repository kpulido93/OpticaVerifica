#!/bin/bash
# Optima Verifica - Git Setup and Initial Commit

set -e

echo "📦 Setting up Git repository..."

# Initialize git if not already done
if [ ! -d ".git" ]; then
    git init
fi

# Create .gitignore
cat > .gitignore << 'EOF'
# Dependencies
node_modules/
**/node_modules/

# Build outputs
.next/
out/
dist/
bin/
obj/

# Environment files
.env
.env.local
.env.*.local

# IDE
.idea/
.vscode/
*.suo
*.user
*.sln.docstates

# OS
.DS_Store
Thumbs.db

# Logs
*.log
npm-debug.log*
yarn-debug.log*
yarn-error.log*

# Test coverage
coverage/

# Temporary files
tmp/
temp/
*.tmp

# Docker
docker-compose.override.yml
EOF

# Add all files
git add -A

# Create initial commit
git commit -m "Initial commit: Optima Verifica MVP

Features:
- ASP.NET Core 8 Minimal API with Basic Auth and Roles
- Next.js 14 Frontend with Dark Theme
- 3 Hardcoded Presets (TSS, Compañeros Salario, Vehículos)
- Job System with background processing (Worker)
- Export to CSV/XLSX/JSON
- Admin Preset Designer (Visual AST Builder)
- MySQL Database Schema with Migrations
- Docker Compose setup

Security:
- Whitelist-based table/column access
- Parameterized queries (no SQL concatenation)
- Role-based access control (ADMIN/OPERATOR/READER)
- Bulk ID processing via temp tables

Structure:
- /api - ASP.NET Core 8 API
- /worker - Background Job Processor
- /frontend - Next.js Frontend
- /db - Migrations and Seeds
- /tests - Unit Tests"

echo "✅ Git repository ready with initial commit!"
echo ""
echo "To push to remote:"
echo "  git remote add origin <your-repo-url>"
echo "  git push -u origin main"
