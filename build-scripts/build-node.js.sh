#!/bin/bash

cd ../html || exit 1
npm ci
npm run prod
cd ..
read -p "Press [Enter] to continue..."
