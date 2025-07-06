#!/bin/bash

# JWT Key Generation Script for Immich Album Downloader
# This script generates a cryptographically secure JWT secret key

echo "🔐 Generating secure JWT secret key..."
echo ""

# Generate a 256-bit (32 byte) random key encoded in base64
JWT_KEY=$(openssl rand -base64 32)

echo "✅ Generated JWT Secret Key:"
echo "JWT_SECRET_KEY=$JWT_KEY"
echo ""

echo "📋 Next steps:"
echo "1. Copy the JWT_SECRET_KEY value above"
echo "2. Add it to your .env file or environment variables"
echo "3. NEVER commit this key to version control"
echo "4. Use a different key for each environment (dev, staging, production)"
echo ""

echo "🔒 Security reminders:"
echo "- This key is used to sign JWT tokens"
echo "- Anyone with this key can create valid tokens"
echo "- Store it securely (e.g., in a secrets manager)"
echo "- Rotate it periodically for enhanced security"
echo ""

# Create a sample .env file if it doesn't exist
if [ ! -f .env ]; then
    echo "📝 Creating .env file with generated key..."
    cat > .env << EOF
# Generated JWT Secret Key - $(date)
JWT_SECRET_KEY=$JWT_KEY

# Other environment variables
ASPNETCORE_ENVIRONMENT=Development
JWT_ISSUER=ImmichDownloader
JWT_AUDIENCE=ImmichDownloader
JWT_TOKEN_LIFETIME_HOURS=24
EOF
    echo "✅ Created .env file with secure JWT key"
else
    echo "ℹ️  .env file already exists - manually update JWT_SECRET_KEY"
fi

echo ""
echo "🚀 Ready to start the application with secure JWT configuration!"