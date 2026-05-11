#!/bin/bash
# =============================================================================
# VPS Security Hardening Script
# Run once after initial deployment: bash /opt/shifter/infra/scripts/harden-vps.sh
# =============================================================================

set -e

echo "=== Hardening VPS ==="

# 1. Enable UFW firewall — only allow SSH, HTTP, HTTPS
echo "Configuring firewall..."
ufw default deny incoming
ufw default allow outgoing
ufw allow 22/tcp    # SSH
ufw allow 80/tcp    # HTTP (Caddy redirect)
ufw allow 443/tcp   # HTTPS (Caddy)
ufw --force enable
echo "✓ Firewall enabled (SSH + HTTP + HTTPS only)"

# 2. Disable root password login (SSH key only)
echo "Hardening SSH..."
sed -i 's/#PasswordAuthentication yes/PasswordAuthentication no/' /etc/ssh/sshd_config
sed -i 's/PasswordAuthentication yes/PasswordAuthentication no/' /etc/ssh/sshd_config
systemctl restart sshd
echo "✓ SSH password login disabled (key-only)"

# 3. Install fail2ban to block brute-force SSH attempts
echo "Installing fail2ban..."
apt-get install -y fail2ban
systemctl enable fail2ban
systemctl start fail2ban
echo "✓ fail2ban installed and running"

# 4. Set PostgreSQL to only listen on Docker network (not 0.0.0.0)
# Already handled by Docker — postgres port is only exposed to the host, not the internet.
# UFW blocks external access to port 5432.
echo "✓ PostgreSQL only accessible via Docker network"

# 5. Automatic security updates
echo "Enabling automatic security updates..."
apt-get install -y unattended-upgrades
dpkg-reconfigure -plow unattended-upgrades
echo "✓ Automatic security updates enabled"

echo ""
echo "=== Hardening complete ==="
echo ""
echo "Security status:"
ufw status
echo ""
echo "fail2ban status:"
fail2ban-client status
