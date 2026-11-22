# Weg's Cafe — Point of Sale & Inventory Management System

A cross-platform **Point of Sale and Inventory Management System** built with **.NET MAUI (C# / XAML)** for **Android Phones and Android Tablets**. Designed specifically for cafe counter operations, the application pairs an offline-first local SQLite engine with **Firebase Realtime Database** and **Cloudinary**, sharing a unified `snake_case` data schema with a companion **Laravel** web administration portal.

---

## Table of Contents

- [Core Features](#core-features)
- [System Architecture & Laravel Interoperability](#system-architecture--laravel-interoperability)
- [Technology Stack](#technology-stack)
- [Database Schema](#database-schema)
- [Project Structure](#project-structure)
- [Setup & Installation](#setup--installation)
- [Firebase Security Rules](#firebase-security-rules)
- [License](#license)

---

## Core Features

### 1. Point of Sale & Barista Counter Workflow
- **Adaptive Responsive Layouts:** Split-screen catalog and live order summary on **Android Tablets**, and a virtualized product grid with an animated slide-up cart drawer on **Android Phones**.
- **Barista Drink Customization:** Built-in modifier modal for beverages supporting **Cup Size** (`Regular`, `Large`), **Ice Level** (`Regular Ice`, `Less Ice`, `No Ice`), **Sweetness** (`100%`, `75%`, `50%`, `0%`), and **Add-ons** (`Extra Shot`, `Oat Milk`, `Caramel Drizzle`).
- **Live Preparation Queue:** Real-time counter ticker tracking paid orders (`Preparing` -> `Ready` -> `Completed`).
- **Hold / Park Orders:** Save open customer tabs locally to clear the register during peak queues and resume them in one tap.
- **1-Tap Availability Toggle:** Long-press any menu tile to mark an item unavailable mid-rush across all connected devices.

### 2. Multi-Payment Processing & Receipt Printing
- **Supported Payment Methods:**
  - **Cash:** Quick-denomination chips (`Exact`, `₱100`, `₱200`, `₱500`, `₱1000`) with automatic change calculation.
  - **GCash:** Enforces transaction reference number validation (`6–20` alphanumeric characters).
  - **Credit Card:** Enforces approval/trace reference validation (`4–20` alphanumeric characters).
- **Bluetooth Thermal Printing:** Integrates with **58mm and 80mm ESC/POS Bluetooth thermal printers** (with automatic printer reconnection) alongside an on-screen digital receipt preview and PDF share fallback.
- **Recent Receipts & PIN-Protected Voiding:** Reprint receipts in one tap or void completed orders using Owner/Head Barista PIN authorization, automatically restoring deducted stock.

### 3. Product Registration & Cloudinary Media Pipeline
- Capture photos via device camera or gallery, automatically downscale and compress to `600x600` JPEG (`80%` quality) off the UI thread, and upload via **Cloudinary's Unsigned REST API**.
- Saves optimized CDN URLs (`image_url`) to Firebase Realtime Database and serves cached `300x300` thumbnails in the Point of Sale grid for instant scrolling.

### 4. Real-Time Inventory Management
- Track live stock quantities, set per-item low-stock thresholds, and filter items by status (`All Items`, `Low Stock`, `Out of Stock`).
- **Stock-Guard Cart Protection:** Prevents adding quantities exceeding available stock.
- **Audit-Ready Stock Movements:** Logs every sale deduction, stock replenishment, spoilage/waste report, and manual adjustment to `inventory_logs`.

### 5. Role-Based Access Control & Quick-Switch PINs
- **Owner-Configured User Levels:** Owners register staff accounts (e.g., `Barista`, `Head Barista`) using a secondary Firebase Authentication instance—keeping the Owner logged in—and assign granular permission flags:
  - `can_access_point_of_sale`
  - `can_view_inventory`
  - `can_restock_inventory`
  - `can_register_products`
  - `can_void_orders`
- **2-Second Barista PIN Switcher:** Overlay numeric PIN pad allowing multiple baristas sharing a counter tablet to switch active cashier sessions instantaneously.

### 6. Offline-First Engine & Automatic Wi-Fi Sync
- **Stale-While-Revalidate Startup:** Boots immediately from local SQLite (`< 200ms`) without blocking on network calls.
- **FIFO Outbox Synchronization:** Queues offline orders, inventory deductions, and product photo uploads locally when Wi-Fi drops, then automatically flushes all pending operations in chronological order upon reconnection.

---

## System Architecture & Laravel Interoperability

This application is engineered to run alongside a **Laravel** web backend connected to the same Firebase Realtime Database via `kreait/laravel-firebase`.

```mermaid
flowchart LR
    subgraph MobileApp [".NET MAUI (Android Phone & Tablet)"]
        UI[Monochrome XAML Views]
        VM[MVVM ViewModels]
        SQL[(Local SQLite Cache & Outbox)]
        SYNC[CloudSynchronizationService]
    end

    subgraph CloudServices ["Cloud Infrastructure"]
        FB[(Firebase Realtime Database)]
        AUTH[Firebase Authentication]
        CLD[Cloudinary Image CDN]
    end

    subgraph WebApp ["Companion Web Portal"]
        LAR[Laravel Application]
    end

    UI <--> VM
    VM <--> SQL
    SQL <--> SYNC
    SYNC <--> FB
    SYNC <--> AUTH
    SYNC <--> CLD
    LAR <--> FB
    LAR --> CLD
