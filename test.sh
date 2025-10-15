#!/bin/bash
echo "{"
echo '  "AuthService": ' $(curl -sk https://localhost/api/auth/health | jq -c) ','
echo '  "UserService": ' $(curl -sk https://localhost/api/user/health | jq -c) ','
echo '  "LoggerService": ' $(curl -sk https://localhost/api/logger/health | jq -c)
echo "}"

