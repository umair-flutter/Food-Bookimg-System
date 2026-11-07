# Foodie Hub – Online Food Booking Website (ASP.NET Core + SQL Server)

A food ordering website with a separate admin panel, built on **ASP.NET Core 10 MVC**, **SQL Server**, **Entity Framework Core** and **Bootstrap 5**.

| Project | What it is | Local URL |
|---|---|---|
| `Food Booking Application` | **Front** website for customers | http://localhost:5263 |
| `FoodBooking.Admin` | **Admin** panel (deploy on `admin.yourdomain.com`) | http://localhost:5264 |
| `FoodBooking.Core` | Shared database, business logic and integrations | – |
| `uploads/` | Shared image folder (logos, products, banners) used by both sites | – |
| `database/` | SQL install script | – |

**Default admin login:** `admin` / `software` (the dashboard reminds you to change it).
**Demo customer:** `demo@customer.test` / `demo123`.
**Demo coupons:** `WELCOME10` (10% off over $15), `FLAT5` ($5 off over $30).

---

## Features

### Front website
- Landing page: hero slider, category tiles, product sections per tag (Best Sellers, Latest, Chef's Special, Today's Deals…), promo banners, brands, recently viewed
- Customer registration, login, logout, **forgot password** (emailed reset link), **change password**, profile and saved address
- **Guest checkout** (can be switched off)
- Search by **category**, **brand**, **keyword** (with live suggestions), tag, price range, vegetarian; sorting and paging
- Product detail: responsive gallery with **zoom**, options/add-ons with live price (size, spice level, toppings), special instructions, specifications, related and **recently viewed** products
- Shopping cart: slide-out mini cart, quantity changes, coupons, tips, free-delivery progress bar
- Delivery or pickup, **ASAP or scheduled time slots** based on opening hours and holidays
- Payment: **Cash on Delivery**, **Credit Card (Clover gateway)**, **PayPal**, plus a **demo/test mode**
- Printable **order invoice**, order **tracking** with live status timeline, **order history**, cancel a pending order, re-order
- CMS pages (About, Terms, Privacy…), contact form, newsletter sign-up, social links, WhatsApp button, SEO meta tags and product structured data

### Admin panel
- **Dashboard**: sales charts, today and this month, pending orders, top products, low stock, setup warnings
- **New-order alert**: bell counter and alarm sound on every admin page
- **Kitchen display**: live board (New → Accepted → Preparing → Ready → Done)
- **Orders**: filters, details, status updates (customer notified by email + SMS), payment status, print invoice, resend invoice, send/re-send to **Clover POS**, CSV export
- **Catalog**: products (multiple images, tags, options with prices, specifications, stock, SEO, Clover item ID, duplicate, show/hide), categories (with sub-categories), brands, tags, attributes and options
- **Holiday setup** and weekly **opening hours**
- **Content management**: create any static page with a rich-text editor and choose whether it appears in the header or footer
- **Banners and ads**: home slider, home middle/bottom, header strip, menu page, product page side, cart page, with optional start/end dates
- **Coupons**: percentage or fixed amount, minimum order, maximum discount, dates, usage limit
- **Reports**: customer management (block/activate, order history, CSV), order report (by day/week/month, payment method, type, status), product sales
- **Website configuration**: logo setup (header, footer, favicon), company setup, SEO meta tags, social media links, ordering and delivery rules, payments, Clover POS, email and SMS
- Contact messages, newsletter subscribers (CSV), **notification log** (every email/SMS/Clover call and its result)

---

## Run locally (Visual Studio 2022+/2026 or the `dotnet` CLI)

Requirements: **.NET 10 SDK** and **SQL Server** (Express, LocalDB or full).

1. Open `Food Booking Application.slnx`.
2. Check the connection string in **both** `Food Booking Application/appsettings.json` and `FoodBooking.Admin/appsettings.json`:
   ```json
   "DefaultConnection": "Server=.\\SQLEXPRESS;Database=FoodBookingDb;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true"
   ```
3. Start both projects (in Visual Studio: right-click the solution → *Configure Startup Projects* → *Multiple*).
   With the CLI:
   ```bash
   dotnet run --project "Food Booking Application" --launch-profile http
   ```
   ```bash
   dotnet run --project FoodBooking.Admin --launch-profile http
   ```
4. On first start the database is **created automatically and filled with demo data** (menu, banners, pages, coupons, demo orders, admin user).

---

## Payments – demo mode

Out of the box **Settings → Payments → "Demo / test mode" is ON**:
- **Cash on Delivery**: order is created as *Unpaid*. It becomes *Paid* when the order is marked *Delivered*.
- **Credit Card** and **PayPal**: no card form or PayPal window appears. The order is created as *Paid* with a `TEST-…` transaction ID. **No money is charged.**

To go live, enter your keys in **Settings → Payments** and turn demo mode **off**:
- **Clover card payments**: public key (PAKMS) and private token from Clover → Ecommerce → API tokens. Customers type their card into Clover's secure iframe, so card numbers never reach your server.
- **PayPal**: Client ID and Secret from developer.paypal.com. Untick *Sandbox* for real payments.

## Clover POS (kitchen)
**Settings → Clover POS**: enable the integration, then enter the Merchant ID and an API token with *Orders read/write* permission.
Every web order is created in Clover as an open order with its items, options, notes, delivery fee and tip, and a **print event** sends it to the kitchen printer. Failed syncs show on the order page with a *Re-send to Clover* button.

## Email and SMS
**Settings → Email & SMS**: SMTP for order confirmations/invoices, status updates and password resets; Twilio for SMS on successful orders and status changes. You can also set an alert email and phone for the kitchen or manager. *Test email* and *Test SMS* buttons are included.
While switched off, messages are not sent but are still recorded under **Notification Logs**, so you can see exactly what would have gone out.

---

## Deploy (Windows hosting with Plesk / IIS)

1. Create a SQL Server database and a database user with **db_owner** rights.
2. Either let the app create the tables on first run (recommended, also adds demo data) **or** run `database/FoodBookingDb.schema.sql` in SQL Server Management Studio. The demo data and admin user are then added on first start.
   To regenerate the script: `dotnet run --project FoodBooking.Admin -- --export-sql database/FoodBookingDb.schema.sql`
3. Create the subdomain **admin.yourdomain.com**.
4. Publish both sites:
   ```bash
   dotnet publish "Food Booking Application" -c Release -o publish/front
   ```
   ```bash
   dotnet publish FoodBooking.Admin -c Release -o publish/admin
   ```
5. Upload `publish/front` to your main domain and `publish/admin` to the admin subdomain. The host needs the **ASP.NET Core 10 Hosting Bundle**.
6. In **both** `appsettings.json` files set:
   - `ConnectionStrings:DefaultConnection`, for example `Server=YOUR_SERVER;Database=YOUR_DB;User Id=YOUR_USER;Password=YOUR_PASSWORD;TrustServerCertificate=True;MultipleActiveResultSets=true`
   - `Uploads:PhysicalPath`: the **same absolute folder** for both sites (for example `C:\inetpub\shared\uploads`), writable by the app pool user. This is how images uploaded in the admin appear on the website.
7. Log in to the admin, change the password, then set **Company setup** (including *Website URL* and *time zone*), logo, payments, Clover, email and SMS.
8. Use SSL on both domains.

---

## Project structure
```
FoodBooking.Core/
  Entities/      Catalog.cs, Sales.cs, Site.cs     (all database tables)
  Data/          AppDbContext.cs, DbSeeder.cs      (schema + demo data)
  Services/      PricingService (cart totals, coupons), OrderService (checkout, status, invoice email),
                 StoreHoursService (hours, holidays, time slots), PaymentServices (Clover POS + gateway, PayPal),
                 NotificationService (SMTP, Twilio), FileStorage (uploads), SettingsService
Food Booking Application/   Front MVC site (Controllers, Views, wwwroot/css/site.css, wwwroot/js/site.js)
FoodBooking.Admin/          Admin MVC site (Controllers, Views, wwwroot/css/admin.css, wwwroot/js/admin.js)
uploads/                    Shared images (demo images are generated SVG placeholders)
database/                   SQL install script
```
