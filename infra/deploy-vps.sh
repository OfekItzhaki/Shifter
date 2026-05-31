#!/bin/bash
# =============================================================================
# Shifter VPS Deploy Script
# Run this on a fresh Ubuntu 24.04 VPS to deploy the full application.
# Usage: ssh root@YOUR_VPS_IP 'bash -s' < infra/deploy-vps.sh
# =============================================================================

set -e

echo "=== Shifter VPS Deploy ==="
echo "Installing Docker..."

# Install Docker
curl -fsSL https://get.docker.com | sh
systemctl enable docker
systemctl start docker

# Install Docker Compose plugin
apt-get update -qq
apt-get install -y docker-compose-plugin git

echo "=== Cloning repository ==="
cd /opt
git clone https://github.com/OfekItzhaki/Shifter.git shifter || (cd shifter && git pull)
cd shifter/infra/compose

echo "=== Setting up environment ==="
if [ ! -f .env ]; then
  cp .env.example .env
  # Generate a random JWT secret
  JWT_SECRET=$(openssl rand -base64 48 | tr -d '\n')
  sed -i "s|changeme_jwt_secret_min_32_chars_long|$JWT_SECRET|g" .env
  echo ""
  echo "⚠️  IMPORTANT: Edit /opt/shifter/infra/compose/.env to set:"
  echo "   - Your domain in APP_FRONTEND_BASE_URL and APP_API_BASE_URL"
  echo "   - SendGrid API key (optional, for emails)"
  echo "   - Twilio credentials (optional, for WhatsApp)"
  echo "   - AI API key (optional, for AI features)"
  echo ""
fi

echo "=== Building and starting services ==="
docker compose up -d --build

echo "=== Installing Caddy (reverse proxy with auto-HTTPS) ==="
apt-get install -y debian-keyring debian-archive-keyring apt-transport-https
curl -1sLf 'https://dl.cloudsmith.io/public/caddy/stable/gpg.key' | gpg --dearmor -o /usr/share/keyrings/caddy-stable-archive-keyring.gpg
curl -1sLf 'https://dl.cloudsmith.io/public/caddy/stable/debian.deb.txt' | tee /etc/apt/sources.list.d/caddy-stable.list
apt-get update -qq
apt-get install -y caddy

echo "=== Creating Caddy config ==="
cat > /etc/caddy/Caddyfile << 'CADDYEOF'
# Replace YOUR_DOMAIN with your actual domain
# Caddy automatically provisions HTTPS via Let's Encrypt

YOUR_DOMAIN {
    # Security headers
    header {
        X-Frame-Options "DENY"
        X-Content-Type-Options "nosniff"
        Referrer-Policy "no-referrer"
        Strict-Transport-Security "max-age=63072000; includeSubDomains; preload"
        Permissions-Policy "camera=(), microphone=(), geolocation=()"
        -Server
        -X-Powered-By
    }

    # Maintenance page — served when backend containers are down (502/503/504)
    handle_errors {
        @maintenance expression {err.status_code} in [502, 503, 504]
        handle @maintenance {
            rewrite * /maintenance.html
            file_server {
                root /opt/shifter/infra/compose
            }
        }
    }

    # Frontend
    handle /* {
        reverse_proxy localhost:3000
    }

    # API
    handle /api/* {
        reverse_proxy localhost:5000
    }

    # Health checks
    handle /health {
        reverse_proxy localhost:5000
    }
}
CADDYEOF

echo ""
echo "=== ✅ Deploy complete! ==="
echo ""
echo "Next steps:"
echo "1. Edit /etc/caddy/Caddyfile — replace YOUR_DOMAIN with your domain"
echo "2. Point your domain's A record to this server's IP"
echo "3. Run: systemctl restart caddy"
echo "4. Visit https://YOUR_DOMAIN"
echo ""
echo "Useful commands:"
echo "  docker compose -f /opt/shifter/infra/compose/docker-compose.yml logs -f    # View logs"
echo "  docker compose -f /opt/shifter/infra/compose/docker-compose.yml restart    # Restart"
echo "  docker compose -f /opt/shifter/infra/compose/docker-compose.yml down       # Stop"
echo "  docker compose -f /opt/shifter/infra/compose/docker-compose.yml up -d      # Start"
echo ""
echo "Database backup:"
echo "  docker exec compose-postgres-1 pg_dump -U jobuler -d jobuler > backup.sql"
echo ""
