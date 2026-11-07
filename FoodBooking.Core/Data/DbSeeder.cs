using FoodBooking.Core.Entities;
using FoodBooking.Core.Services;
using Microsoft.EntityFrameworkCore;

namespace FoodBooking.Core.Data;

/// <summary>Creates the database (if missing) and fills it with demo content on first run.</summary>
public static class DbSeeder
{
    public static async Task InitializeAsync(AppDbContext db, FileStorage storage)
    {
        await db.Database.EnsureCreatedAsync();

        if (!await db.AdminUsers.AnyAsync())
        {
            db.AdminUsers.Add(new AdminUser { Username = "admin", PasswordHash = Passwords.Hash("software"), FullName = "Administrator", Email = "admin@example.com" });
            await db.SaveChangesAsync();
        }
        if (!await db.SiteSettings.AnyAsync())
        {
            db.SiteSettings.Add(new SiteSetting
            {
                CompanyName = "Foodie Hub",
                Tagline = "Fresh food, delivered hot",
                Address = "123 Main Street, Springfield",
                Phone = "+1 555 010 2030",
                Email = "hello@foodiehub.test",
                AboutText = "Foodie Hub serves freshly prepared pizzas, burgers, pasta, salads and desserts made with locally sourced ingredients. Order online for fast delivery or pick it up on your way home.",
                FooterText = "Fresh food, made with love and delivered hot to your door.",
                CurrencySymbol = "$", CurrencyCode = "USD",
                TaxPercent = 8.25m, DeliveryCharge = 3.99m, FreeDeliveryAbove = 40m, MinOrderAmount = 10m,
                MetaTitle = "Foodie Hub - Order Food Online | Pizza, Burgers & More",
                MetaDescription = "Order delicious pizza, burgers, pasta, salads and desserts online. Fast delivery, easy pickup and secure payment.",
                MetaKeywords = "food delivery, order food online, pizza, burgers, restaurant",
                FacebookUrl = "https://facebook.com/", InstagramUrl = "https://instagram.com/", TwitterUrl = "https://x.com/", YoutubeUrl = "https://youtube.com/",
                AnnouncementText = "🎉 Use code WELCOME10 for 10% off your first order · Free delivery on orders over $40"
            });
            await db.SaveChangesAsync();
        }
        if (!await db.BusinessHours.AnyAsync())
        {
            foreach (var d in Enum.GetValues<DayOfWeek>())
                db.BusinessHours.Add(new BusinessHour
                {
                    DayOfWeek = d,
                    OpenTime = new TimeSpan(10, 0, 0),
                    CloseTime = d is DayOfWeek.Friday or DayOfWeek.Saturday ? new TimeSpan(23, 30, 0) : new TimeSpan(22, 0, 0)
                });
            db.Holidays.Add(new Holiday { Date = new DateTime(DateTime.UtcNow.Year, 12, 25), Title = "Christmas Day", Message = "Merry Christmas! We are closed today.", IsClosedAllDay = true });
            db.Holidays.Add(new Holiday { Date = new DateTime(DateTime.UtcNow.Year + 1, 1, 1), Title = "New Year's Day", Message = "Happy New Year! Open with special hours.", IsClosedAllDay = false, OpenTime = new TimeSpan(14, 0, 0), CloseTime = new TimeSpan(21, 0, 0) });
            await db.SaveChangesAsync();
        }
        if (!await db.Categories.AnyAsync()) await SeedCatalogAsync(db, storage);
        if (!await db.Banners.AnyAsync()) await SeedBannersAsync(db, storage);
        if (!await db.CmsPages.AnyAsync()) await SeedPagesAsync(db);
        if (!await db.Coupons.AnyAsync())
        {
            db.Coupons.AddRange(
                new Coupon { Code = "WELCOME10", Description = "10% off your order", DiscountType = DiscountType.Percentage, Value = 10, MinOrderAmount = 15, MaxDiscount = 20 },
                new Coupon { Code = "FLAT5", Description = "$5 off orders over $30", DiscountType = DiscountType.FixedAmount, Value = 5, MinOrderAmount = 30 });
            await db.SaveChangesAsync();
        }
        if (!await db.Customers.AnyAsync()) await SeedCustomersAndOrdersAsync(db);
    }

    private record P(string Name, string Cat, string? Brand, decimal Price, decimal? Sale, string Emoji, string Short, string[] Tags, bool Veg = false, bool Spicy = false, string? Attr = null);

    private static async Task SeedCatalogAsync(AppDbContext db, FileStorage storage)
    {
        var cats = new (string name, string emoji, string c1, string c2, string desc)[]
        {
            ("Pizza", "🍕", "#ff7b54", "#e63946", "Stone-baked pizzas with hand-stretched dough"),
            ("Burgers", "🍔", "#f4a261", "#d62828", "Juicy flame-grilled burgers"),
            ("Pasta", "🍝", "#ffb703", "#fb8500", "Fresh pasta tossed in house-made sauces"),
            ("Salads", "🥗", "#80ed99", "#2d6a4f", "Crisp, healthy and colourful bowls"),
            ("Wraps & Rolls", "🌯", "#e9c46a", "#bc6c25", "Loaded wraps and rolls on the go"),
            ("Starters", "🍟", "#ffd166", "#ef476f", "Perfect for sharing"),
            ("Desserts", "🍰", "#ffafcc", "#c9184a", "Sweet treats to finish"),
            ("Drinks", "🥤", "#90e0ef", "#0077b6", "Shakes, juices and soft drinks"),
        };
        var catMap = new Dictionary<string, Category>();
        int i = 0;
        foreach (var c in cats)
        {
            var slug = Slug.Create(c.name);
            var cat = new Category
            {
                Name = c.name, Slug = slug, Icon = c.emoji, Description = c.desc, SortOrder = i++,
                ImageUrl = storage.WritePlaceholder("categories", slug + ".svg", c.name, c.emoji, c.c1, c.c2, 600, 600)
            };
            catMap[c.name] = cat; db.Categories.Add(cat);
        }

        var brands = new (string name, string emoji, string c1, string c2)[]
        {
            ("Chef's Kitchen", "👨‍🍳", "#264653", "#2a9d8f"), ("Grill Masters", "🔥", "#d62828", "#f77f00"),
            ("Green Garden", "🌿", "#52b788", "#1b4332"), ("Sugar Rush Bakery", "🧁", "#ff8fab", "#9d4edd"),
            ("Brew & Co", "☕", "#6f4e37", "#b08968"), ("Napoli Ovens", "🇮🇹", "#2a9d8f", "#e76f51"),
        };
        var brandMap = new Dictionary<string, Brand>();
        i = 0;
        foreach (var b in brands)
        {
            var slug = Slug.Create(b.name);
            var brand = new Brand { Name = b.name, Slug = slug, SortOrder = i++, LogoUrl = storage.WritePlaceholder("brands", slug + ".svg", b.name, b.emoji, b.c1, b.c2, 400, 300) };
            brandMap[b.name] = brand; db.Brands.Add(brand);
        }

        var tags = new[]
        {
            new Tag { Name = "Best Sellers", Slug = "best-sellers", Subtitle = "Our customers' all-time favourites", BadgeColor = "#e63946", SortOrder = 1 },
            new Tag { Name = "Latest", Slug = "latest", Subtitle = "Fresh additions to our menu", BadgeColor = "#2a9d8f", SortOrder = 2 },
            new Tag { Name = "New", Slug = "new", Subtitle = "Just landed on the menu", BadgeColor = "#3a86ff", ShowOnHome = false, SortOrder = 3 },
            new Tag { Name = "Chef's Special", Slug = "chefs-special", Subtitle = "Hand-picked by our head chef", BadgeColor = "#ff9f1c", SortOrder = 4 },
            new Tag { Name = "Today's Deals", Slug = "todays-deals", Subtitle = "Limited-time prices", BadgeColor = "#8338ec", SortOrder = 5 },
        };
        db.Tags.AddRange(tags);
        var tagMap = tags.ToDictionary(t => t.Slug);

        var size = new ProductAttribute { Name = "Size", InputType = AttributeInputType.SingleSelect, IsRequired = true, SortOrder = 1 };
        size.Values.AddRange([new() { Value = "Small (8\")", SortOrder = 1 }, new() { Value = "Medium (12\")", SortOrder = 2 }, new() { Value = "Large (16\")", SortOrder = 3 }]);
        var spice = new ProductAttribute { Name = "Spice Level", InputType = AttributeInputType.SingleSelect, IsRequired = false, SortOrder = 2 };
        spice.Values.AddRange([new() { Value = "Mild", SortOrder = 1 }, new() { Value = "Medium", SortOrder = 2 }, new() { Value = "Hot", SortOrder = 3 }]);
        var toppings = new ProductAttribute { Name = "Extra Toppings", InputType = AttributeInputType.MultiSelect, SortOrder = 3 };
        toppings.Values.AddRange([new() { Value = "Extra Cheese", SortOrder = 1 }, new() { Value = "Mushrooms", SortOrder = 2 }, new() { Value = "Olives", SortOrder = 3 }, new() { Value = "Jalapeños", SortOrder = 4 }, new() { Value = "Bacon", SortOrder = 5 }]);
        var burgerExtras = new ProductAttribute { Name = "Burger Add-ons", InputType = AttributeInputType.MultiSelect, SortOrder = 4 };
        burgerExtras.Values.AddRange([new() { Value = "Extra Patty", SortOrder = 1 }, new() { Value = "Cheddar Slice", SortOrder = 2 }, new() { Value = "Fried Egg", SortOrder = 3 }, new() { Value = "Avocado", SortOrder = 4 }]);
        var drinkSize = new ProductAttribute { Name = "Cup Size", InputType = AttributeInputType.SingleSelect, IsRequired = true, SortOrder = 5 };
        drinkSize.Values.AddRange([new() { Value = "Regular", SortOrder = 1 }, new() { Value = "Large", SortOrder = 2 }]);
        db.ProductAttributes.AddRange(size, spice, toppings, burgerExtras, drinkSize);
        await db.SaveChangesAsync();

        var products = new P[]
        {
            new("Margherita Pizza", "Pizza", "Napoli Ovens", 11.99m, null, "🍕", "San Marzano tomato, fior di latte mozzarella and fresh basil.", ["best-sellers", "latest"], Veg: true, Attr: "pizza"),
            new("Pepperoni Feast", "Pizza", "Napoli Ovens", 14.49m, 12.99m, "🍕", "Double pepperoni, mozzarella and oregano on a crispy base.", ["best-sellers", "todays-deals"], Attr: "pizza"),
            new("BBQ Chicken Pizza", "Pizza", "Chef's Kitchen", 15.99m, null, "🍕", "Smoky BBQ sauce, grilled chicken, red onion and coriander.", ["chefs-special", "new"], Attr: "pizza"),
            new("Veggie Supreme", "Pizza", "Napoli Ovens", 13.49m, null, "🫑", "Peppers, mushrooms, olives, onions and sweetcorn.", ["latest"], Veg: true, Attr: "pizza"),
            new("Classic Cheeseburger", "Burgers", "Grill Masters", 9.99m, null, "🍔", "Flame-grilled beef patty, cheddar, pickles and house sauce.", ["best-sellers"], Attr: "burger"),
            new("Double Smash Burger", "Burgers", "Grill Masters", 13.99m, 11.99m, "🍔", "Two smashed patties, American cheese, caramelised onions.", ["todays-deals", "chefs-special"], Attr: "burger"),
            new("Spicy Chicken Burger", "Burgers", "Grill Masters", 10.99m, null, "🌶️", "Crispy fried chicken, sriracha mayo and slaw.", ["latest", "new"], Spicy: true, Attr: "burger-spicy"),
            new("Garden Veggie Burger", "Burgers", "Green Garden", 10.49m, null, "🥬", "Chickpea & quinoa patty, avocado, tomato and lettuce.", ["latest"], Veg: true, Attr: "burger"),
            new("Spaghetti Carbonara", "Pasta", "Chef's Kitchen", 12.99m, null, "🍝", "Guanciale, egg yolk, pecorino and black pepper.", ["chefs-special", "best-sellers"]),
            new("Penne Arrabbiata", "Pasta", "Chef's Kitchen", 11.49m, null, "🌶️", "Penne in a fiery tomato, garlic and chilli sauce.", ["latest"], Veg: true, Spicy: true, Attr: "spice"),
            new("Creamy Alfredo", "Pasta", "Chef's Kitchen", 12.49m, 10.99m, "🍝", "Fettuccine in parmesan cream sauce with grilled chicken.", ["todays-deals"]),
            new("Caesar Salad", "Salads", "Green Garden", 8.99m, null, "🥗", "Romaine, parmesan, croutons and classic Caesar dressing.", ["best-sellers"]),
            new("Greek Salad", "Salads", "Green Garden", 8.49m, null, "🥗", "Tomato, cucumber, feta, olives and red onion.", ["latest"], Veg: true),
            new("Quinoa Power Bowl", "Salads", "Green Garden", 10.99m, null, "🥑", "Quinoa, avocado, roasted veg, chickpeas and tahini.", ["new", "chefs-special"], Veg: true),
            new("Chicken Shawarma Wrap", "Wraps & Rolls", "Grill Masters", 9.49m, null, "🌯", "Marinated chicken, garlic sauce, pickles and fries.", ["best-sellers"], Attr: "spice"),
            new("Falafel Wrap", "Wraps & Rolls", "Green Garden", 8.49m, null, "🧆", "Crispy falafel, hummus, salad and tahini.", ["latest"], Veg: true),
            new("Loaded Fries", "Starters", "Grill Masters", 6.99m, null, "🍟", "Fries with cheese sauce, bacon bits and spring onion.", ["best-sellers", "todays-deals"]),
            new("Chicken Wings (8 pc)", "Starters", "Grill Masters", 9.99m, null, "🍗", "Crispy wings tossed in your choice of sauce.", ["chefs-special"], Spicy: true, Attr: "spice"),
            new("Garlic Bread", "Starters", "Napoli Ovens", 4.99m, null, "🥖", "Toasted ciabatta with garlic butter and herbs.", ["latest"], Veg: true),
            new("Chocolate Lava Cake", "Desserts", "Sugar Rush Bakery", 6.49m, null, "🍫", "Warm chocolate cake with a molten centre.", ["best-sellers", "chefs-special"], Veg: true),
            new("New York Cheesecake", "Desserts", "Sugar Rush Bakery", 5.99m, null, "🍰", "Creamy baked cheesecake with berry compote.", ["latest"], Veg: true),
            new("Strawberry Milkshake", "Drinks", "Brew & Co", 4.99m, null, "🥤", "Thick shake with fresh strawberries and vanilla ice cream.", ["best-sellers"], Veg: true, Attr: "drink"),
            new("Iced Caramel Latte", "Drinks", "Brew & Co", 4.49m, 3.99m, "🧋", "Espresso, milk and caramel over ice.", ["todays-deals", "new"], Veg: true, Attr: "drink"),
            new("Fresh Orange Juice", "Drinks", "Brew & Co", 3.99m, null, "🍊", "Freshly squeezed oranges, nothing else.", ["latest"], Veg: true, Attr: "drink"),
        };

        var palette = new (string, string)[] { ("#ff9a8b", "#e63946"), ("#f6d365", "#fda085"), ("#84fab0", "#2a9d8f"), ("#a1c4fd", "#3a86ff"), ("#fbc2eb", "#a18cd1"), ("#ffecd2", "#fcb69f") };
        var sizeVals = size.Values.OrderBy(v => v.SortOrder).ToList();
        var spiceVals = spice.Values.OrderBy(v => v.SortOrder).ToList();
        var topVals = toppings.Values.OrderBy(v => v.SortOrder).ToList();
        var burgerVals = burgerExtras.Values.OrderBy(v => v.SortOrder).ToList();
        var drinkVals = drinkSize.Values.OrderBy(v => v.SortOrder).ToList();
        int n = 0;
        foreach (var p in products)
        {
            var slug = Slug.Create(p.Name);
            var (c1, c2) = palette[n % palette.Length];
            var prod = new Product
            {
                Name = p.Name, Slug = slug, Sku = "FB-" + (1001 + n), Price = p.Price, SalePrice = p.Sale,
                Category = catMap[p.Cat], Brand = p.Brand == null ? null : brandMap[p.Brand],
                ShortDescription = p.Short,
                Description = $"<p>{p.Short}</p><p>Every order is prepared fresh in our kitchen the moment you order it, using quality ingredients from trusted local suppliers. Perfect for lunch, dinner or a late-night treat.</p><ul><li>Made fresh to order</li><li>Packed to stay hot</li><li>Allergen information available on request</li></ul>",
                IsVegetarian = p.Veg, IsSpicy = p.Spicy, PrepTimeMinutes = 15 + (n % 4) * 5, SortOrder = n,
                ViewCount = 50 + n * 7 % 90, SoldCount = 20 + n * 13 % 120,
                CreatedAt = DateTime.UtcNow.AddDays(-n)
            };
            prod.Images.Add(new ProductImage { Url = storage.WritePlaceholder("products", slug + "-1.svg", p.Name, p.Emoji, c1, c2), SortOrder = 0 });
            prod.Images.Add(new ProductImage { Url = storage.WritePlaceholder("products", slug + "-2.svg", p.Name, p.Emoji, c2, c1), SortOrder = 1 });
            prod.Images.Add(new ProductImage { Url = storage.WritePlaceholder("products", slug + "-3.svg", p.Name, "😋", "#2b2d42", c2), SortOrder = 2 });
            foreach (var t in p.Tags) prod.ProductTags.Add(new ProductTag { Tag = tagMap[t] });

            prod.Specifications.Add(new ProductSpecification { Name = "Calories", Value = $"{350 + n * 37 % 500} kcal", SortOrder = 1 });
            prod.Specifications.Add(new ProductSpecification { Name = "Serves", Value = p.Cat == "Drinks" || p.Cat == "Desserts" ? "1 person" : "1-2 people", SortOrder = 2 });
            prod.Specifications.Add(new ProductSpecification { Name = "Preparation time", Value = $"{prod.PrepTimeMinutes} minutes", SortOrder = 3 });
            prod.Specifications.Add(new ProductSpecification { Name = "Dietary", Value = p.Veg ? "Vegetarian" : "Contains meat", SortOrder = 4 });

            switch (p.Attr)
            {
                case "pizza":
                    prod.Options.Add(new ProductOption { AttributeValue = sizeVals[0], PriceAdjustment = -2m });
                    prod.Options.Add(new ProductOption { AttributeValue = sizeVals[1], PriceAdjustment = 0m, IsDefault = true });
                    prod.Options.Add(new ProductOption { AttributeValue = sizeVals[2], PriceAdjustment = 4m });
                    prod.Options.Add(new ProductOption { AttributeValue = topVals[0], PriceAdjustment = 1.5m });
                    prod.Options.Add(new ProductOption { AttributeValue = topVals[1], PriceAdjustment = 1m });
                    prod.Options.Add(new ProductOption { AttributeValue = topVals[2], PriceAdjustment = 1m });
                    prod.Options.Add(new ProductOption { AttributeValue = topVals[3], PriceAdjustment = 0.75m });
                    if (!p.Veg) prod.Options.Add(new ProductOption { AttributeValue = topVals[4], PriceAdjustment = 2m });
                    break;
                case "burger":
                case "burger-spicy":
                    foreach (var (v, price) in burgerVals.Zip(new[] { 3m, 1m, 1.25m, 1.5m })) prod.Options.Add(new ProductOption { AttributeValue = v, PriceAdjustment = price });
                    if (p.Attr == "burger-spicy") goto case "spice";
                    break;
                case "spice":
                    prod.Options.Add(new ProductOption { AttributeValue = spiceVals[0], PriceAdjustment = 0, IsDefault = true });
                    prod.Options.Add(new ProductOption { AttributeValue = spiceVals[1], PriceAdjustment = 0 });
                    prod.Options.Add(new ProductOption { AttributeValue = spiceVals[2], PriceAdjustment = 0 });
                    break;
                case "drink":
                    prod.Options.Add(new ProductOption { AttributeValue = drinkVals[0], PriceAdjustment = 0, IsDefault = true });
                    prod.Options.Add(new ProductOption { AttributeValue = drinkVals[1], PriceAdjustment = 1.25m });
                    break;
            }
            db.Products.Add(prod);
            n++;
        }
        await db.SaveChangesAsync();
    }

    private static async Task SeedBannersAsync(AppDbContext db, FileStorage storage)
    {
        db.Banners.AddRange(
            new Banner
            {
                Title = "Hot & Fresh Pizza Delivered", SubTitle = "Stone-baked to perfection and at your door in 30 minutes.",
                ButtonText = "Order Now", LinkUrl = "/category/pizza", Position = BannerPosition.HomeSlider, SortOrder = 1,
                ImageUrl = storage.WritePlaceholder("banners", "slide-pizza.svg", "", "🍕", "#e63946", "#ff7b54", 1600, 640)
            },
            new Banner
            {
                Title = "Burgers Worth the Bite", SubTitle = "Flame-grilled, stacked high and full of flavour.",
                ButtonText = "See Burgers", LinkUrl = "/category/burgers", Position = BannerPosition.HomeSlider, SortOrder = 2,
                ImageUrl = storage.WritePlaceholder("banners", "slide-burger.svg", "", "🍔", "#d62828", "#f77f00", 1600, 640)
            },
            new Banner
            {
                Title = "Eat Green, Feel Great", SubTitle = "Colourful salads and bowls packed with goodness.",
                ButtonText = "Explore Salads", LinkUrl = "/category/salads", Position = BannerPosition.HomeSlider, SortOrder = 3,
                ImageUrl = storage.WritePlaceholder("banners", "slide-salad.svg", "", "🥗", "#1b4332", "#52b788", 1600, 640)
            },
            new Banner
            {
                Title = "Get 10% off your first order", SubTitle = "Use code WELCOME10 at checkout",
                ButtonText = "Start Ordering", LinkUrl = "/menu", Position = BannerPosition.HomeMiddle, SortOrder = 1,
                ImageUrl = storage.WritePlaceholder("banners", "promo-welcome.svg", "", "🎁", "#3a0ca3", "#7209b7", 1600, 400)
            },
            new Banner
            {
                Title = "Sweet endings", SubTitle = "Treat yourself to our freshly baked desserts",
                ButtonText = "Desserts", LinkUrl = "/category/desserts", Position = BannerPosition.HomeBottom, SortOrder = 1,
                ImageUrl = storage.WritePlaceholder("banners", "promo-desserts.svg", "", "🍰", "#ff8fab", "#c9184a", 800, 400)
            },
            new Banner
            {
                Title = "Cool down", SubTitle = "Shakes, juices & iced coffee",
                ButtonText = "Drinks", LinkUrl = "/category/drinks", Position = BannerPosition.HomeBottom, SortOrder = 2,
                ImageUrl = storage.WritePlaceholder("banners", "promo-drinks.svg", "", "🥤", "#0077b6", "#90e0ef", 800, 400)
            },
            new Banner
            {
                Title = "Today's Deals are live!", SubTitle = "Save on selected favourites", ButtonText = "View deals", LinkUrl = "/tag/todays-deals",
                Position = BannerPosition.MenuPageTop, SortOrder = 1,
                ImageUrl = storage.WritePlaceholder("banners", "menu-deals.svg", "", "🔥", "#ff9f1c", "#e63946", 1600, 300)
            },
            new Banner
            {
                Title = "Free delivery over $40", SubTitle = "Add a little more and delivery is on us.", ButtonText = "Keep shopping", LinkUrl = "/menu",
                Position = BannerPosition.ProductPageSide, SortOrder = 1,
                ImageUrl = storage.WritePlaceholder("banners", "side-delivery.svg", "", "🛵", "#2a9d8f", "#264653", 600, 600)
            });
        await db.SaveChangesAsync();
    }

    private static async Task SeedPagesAsync(AppDbContext db)
    {
        db.CmsPages.AddRange(
            new CmsPage
            {
                Title = "About Us", Slug = "about-us", ShowInHeader = true, ShowInFooter = true, SortOrder = 1,
                Content = "<h2>Our story</h2><p>Foodie Hub started as a small neighbourhood kitchen with one simple idea: great food, made fresh, delivered fast. Today we serve thousands of happy customers across the city.</p><h3>What makes us different</h3><ul><li>Everything is cooked to order</li><li>Locally sourced ingredients</li><li>Eco-friendly packaging</li><li>Friendly, fast delivery riders</li></ul>"
            },
            new CmsPage
            {
                Title = "Terms & Conditions", Slug = "terms-and-conditions", SortOrder = 2,
                Content = "<h2>Terms &amp; Conditions</h2><p>By placing an order on this website you agree to the following terms.</p><h4>1. Orders</h4><p>All orders are subject to availability and confirmation. We reserve the right to refuse or cancel any order.</p><h4>2. Prices</h4><p>Prices include applicable taxes unless stated otherwise. Delivery charges are shown at checkout.</p><h4>3. Delivery</h4><p>Delivery times are estimates and may vary depending on demand, weather and traffic.</p><h4>4. Cancellations</h4><p>Orders can be cancelled while they are still pending. Once preparation has started, cancellation may not be possible.</p>"
            },
            new CmsPage
            {
                Title = "Privacy Policy", Slug = "privacy-policy", SortOrder = 3,
                Content = "<h2>Privacy Policy</h2><p>We respect your privacy. We collect only the information needed to process and deliver your order — your name, contact details and delivery address.</p><p>Payment card details are processed directly by our payment provider and are never stored on our servers.</p><p>We never sell your personal data. You can request deletion of your account at any time by contacting us.</p>"
            },
            new CmsPage
            {
                Title = "Refund Policy", Slug = "refund-policy", SortOrder = 4,
                Content = "<h2>Refund Policy</h2><p>If something is wrong with your order, please contact us within 24 hours and we'll make it right with a replacement or refund.</p>"
            },
            new CmsPage
            {
                Title = "FAQ", Slug = "faq", ShowInHeader = false, ShowInFooter = true, SortOrder = 5,
                Content = "<h2>Frequently Asked Questions</h2><h5>How long does delivery take?</h5><p>Most orders arrive within 30–45 minutes.</p><h5>Can I schedule an order?</h5><p>Yes! Choose a later time at checkout.</p><h5>Do I need an account?</h5><p>No, you can check out as a guest. An account lets you track order history and re-order quickly.</p><h5>Which payment methods do you accept?</h5><p>Cash on delivery, credit/debit cards and PayPal.</p>"
            });
        await db.SaveChangesAsync();
    }

    private static async Task SeedCustomersAndOrdersAsync(AppDbContext db)
    {
        var customer = new Customer
        {
            FirstName = "Demo", LastName = "Customer", Email = "demo@customer.test", Phone = "+1 555 123 4567",
            PasswordHash = Passwords.Hash("demo123"), Address = "42 Elm Street", City = "Springfield", PostalCode = "12345",
            CreatedAt = DateTime.UtcNow.AddDays(-30)
        };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        var products = await db.Products.Include(p => p.Images).OrderBy(p => p.Id).ToListAsync();
        var rnd = new Random(42);
        var statuses = new[] { OrderStatus.Delivered, OrderStatus.Delivered, OrderStatus.Delivered, OrderStatus.Cancelled, OrderStatus.Preparing, OrderStatus.Pending };
        for (int i = 0; i < 14; i++)
        {
            var created = DateTime.UtcNow.AddDays(-(13 - i)).AddHours(-rnd.Next(0, 8));
            var status = i >= 12 ? statuses[4 + (i - 12)] : statuses[rnd.Next(0, 4)];
            var type = rnd.Next(0, 2) == 0 ? OrderType.Delivery : OrderType.Pickup;
            var method = (PaymentMethod)rnd.Next(0, 3);
            var o = new Order
            {
                OrderNumber = created.ToString("yyMMdd") + (1000 + i),
                CustomerId = i % 3 == 0 ? null : customer.Id, IsGuest = i % 3 == 0,
                CustomerName = i % 3 == 0 ? $"Guest Buyer {i}" : customer.FullName,
                Email = i % 3 == 0 ? $"guest{i}@example.test" : customer.Email,
                Phone = "+1 555 000 " + (1000 + i),
                Address = type == OrderType.Delivery ? "42 Elm Street" : null, City = type == OrderType.Delivery ? "Springfield" : null,
                OrderType = type, PaymentMethod = method,
                PaymentStatus = method != PaymentMethod.CashOnDelivery || status == OrderStatus.Delivered ? PaymentStatus.Paid : PaymentStatus.Unpaid,
                TransactionId = method != PaymentMethod.CashOnDelivery ? "DEMO-" + Guid.NewGuid().ToString("N")[..10].ToUpper() : null,
                Status = status, KitchenAcknowledged = true, CreatedAt = created
            };
            foreach (var p in products.OrderBy(_ => rnd.Next()).Take(rnd.Next(1, 4)))
            {
                var q = rnd.Next(1, 3);
                o.Items.Add(new OrderItem { ProductId = p.Id, ProductName = p.Name, ImageUrl = p.MainImage, UnitPrice = p.SellingPrice, Quantity = q, LineTotal = p.SellingPrice * q });
            }
            o.SubTotal = o.Items.Sum(x => x.LineTotal);
            o.Tax = Math.Round(o.SubTotal * 0.0825m, 2);
            o.DeliveryCharge = type == OrderType.Delivery && o.SubTotal < 40 ? 3.99m : 0;
            o.Total = o.SubTotal + o.Tax + o.DeliveryCharge;
            o.History.Add(new OrderStatusHistory { Status = OrderStatus.Pending, Note = "Order placed on website.", CreatedAt = created });
            if (status != OrderStatus.Pending) o.History.Add(new OrderStatusHistory { Status = status, CreatedAt = created.AddMinutes(25) });
            db.Orders.Add(o);
        }
        await db.SaveChangesAsync();
    }
}
