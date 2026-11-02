#!/usr/bin/env node
/**
 * seed-api.mjs
 * Seeds Locations, Dishes, and Tables through the backend REST API Gateway.
 * Zero external dependencies (uses native fetch in Node 18+ and 24+).
 *
 * Usage:
 *   node seed-api.mjs <API_BASE_URL>
 *
 * Example:
 *   node seed-api.mjs https://xyz12345.execute-api.eu-north-1.amazonaws.com/develop
 */

import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';

const __filename = fileURLToPath(import.meta.url);
const __dirname = dirname(__filename);

const rawArg = process.argv[2] || process.env.API_BASE_URL;

if (!rawArg) {
  console.error(`
❌ Error: API Base URL is required!

Usage:
  node seed-api.mjs <API_BASE_URL>
  
Or set the environment variable:
  $env:API_BASE_URL = "https://<api-id>.execute-api.<region>.amazonaws.com/<stage>"
  node seed-api.mjs

Where can you get this URL?
  1. From terminal output right after running 'syndicate deploy'
  2. From 'frontend/restaurant-frontend-app/src/environments/environment.development.ts' (backendUrl)
  3. From AWS Console -> API Gateway -> restaurant-backend-api -> Stages (e.g. develop)
`);
  process.exit(1);
}

const API_BASE_URL = rawArg.replace(/\/+$/, '');

function loadJson(relPath) {
  const fullPath = join(__dirname, relPath);
  return JSON.parse(readFileSync(fullPath, 'utf-8'));
}

async function request(endpoint, method, body) {
  const url = `${API_BASE_URL}${endpoint}`;
  const response = await fetch(url, {
    method,
    headers: { 'Content-Type': 'application/json' },
    body: body ? JSON.stringify(body) : undefined,
  });

  const text = await response.text();
  let json;
  try {
    json = JSON.parse(text);
  } catch {
    json = text;
  }

  if (!response.ok) {
    throw new Error(`${method} ${endpoint} returned ${response.status}: ${typeof json === 'string' ? json : JSON.stringify(json)}`);
  }

  return json;
}

async function main() {
  console.log(`===================================================`);
  console.log(`🚀 Starting REST API Seeder`);
  console.log(`   Target Endpoint: ${API_BASE_URL}`);
  console.log(`===================================================\n`);

  const locations = loadJson('data/locations.json');
  const dishes = loadJson('data/dishes.json');
  const tables = loadJson('data/tables.json');

  // Map to hold seed location ID -> newly generated API ID
  const locationIdMap = new Map();

  // 1. Seed Locations
  console.log(`📍 Step 1: Seeding ${locations.length} Locations...`);
  for (const loc of locations) {
    try {
      const payload = {
        address: loc.address,
        description: loc.description,
        totalCapacity: loc.totalCapacity,
        averageOccupancy: loc.averageOccupancy,
        rating: loc.rating,
      };

      const res = await request('/locations', 'POST', payload);
      const newId = res.id;
      locationIdMap.set(loc.id, newId);
      console.log(`  ✓ Created location: "${loc.address}" -> ID: ${newId}`);
    } catch (err) {
      console.error(`  ✗ Failed to create location "${loc.address}":`, err.message);
    }
  }

  // 2. Seed Dishes
  console.log(`\n🍲 Step 2: Seeding ${dishes.length} Dishes...`);
  for (const dish of dishes) {
    try {
      // Remap seed locationIds to actual created location IDs
      const mappedLocationIds = (dish.locationIds || [])
        .map((oldId) => locationIdMap.get(oldId))
        .filter(Boolean);

      const payload = {
        name: dish.name,
        description: dish.description,
        dishType: dish.dishType,
        price: dish.price,
        weight: dish.weight,
        calories: dish.calories,
        proteins: dish.proteins,
        fats: dish.fats,
        carbohydrates: dish.carbohydrates,
        vitamins: dish.vitamins,
        state: dish.state,
        locationIds: mappedLocationIds.length > 0 ? mappedLocationIds : Array.from(locationIdMap.values()),
      };

      const res = await request('/dishes', 'POST', payload);
      console.log(`  ✓ Created dish: "${dish.name}" (${dish.dishType}, $${dish.price}) -> ID: ${res.id}`);
    } catch (err) {
      console.error(`  ✗ Failed to create dish "${dish.name}":`, err.message);
    }
  }

  // 3. Seed Tables
  console.log(`\n🪑 Step 3: Seeding ${tables.length} Tables...`);
  let tablesSuccess = 0;
  for (const table of tables) {
    try {
      const actualLocationId = locationIdMap.get(table.locationId);
      if (!actualLocationId) {
        continue;
      }

      const payload = {
        locationId: actualLocationId,
        capacity: table.capacity,
        tableNumber: table.tableNumber,
      };

      await request('/tables', 'POST', payload);
      tablesSuccess++;
    } catch (err) {
      console.error(`  ✗ Failed to create table #${table.tableNumber}:`, err.message);
    }
  }
  console.log(`  ✓ Created ${tablesSuccess} tables across all locations.`);

  console.log('\n🎉 API Seeding completed successfully!');
}

main().catch((err) => {
  console.error('\n❌ Seeding failed:', err);
  process.exit(1);
});
