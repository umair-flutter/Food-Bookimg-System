CREATE TABLE [AdminUsers] (
    [Id] int NOT NULL IDENTITY,
    [Username] nvarchar(60) NOT NULL,
    [PasswordHash] nvarchar(max) NOT NULL,
    [FullName] nvarchar(120) NULL,
    [Email] nvarchar(150) NULL,
    [IsActive] bit NOT NULL,
    [LastLoginAt] datetime2 NULL,
    CONSTRAINT [PK_AdminUsers] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [Banners] (
    [Id] int NOT NULL IDENTITY,
    [Title] nvarchar(150) NOT NULL,
    [SubTitle] nvarchar(300) NULL,
    [ImageUrl] nvarchar(300) NULL,
    [LinkUrl] nvarchar(300) NULL,
    [ButtonText] nvarchar(60) NULL,
    [Position] int NOT NULL,
    [SortOrder] int NOT NULL,
    [IsActive] bit NOT NULL,
    [StartDate] datetime2 NULL,
    [EndDate] datetime2 NULL,
    CONSTRAINT [PK_Banners] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [Brands] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(100) NOT NULL,
    [Slug] nvarchar(120) NOT NULL,
    [LogoUrl] nvarchar(300) NULL,
    [Description] nvarchar(500) NULL,
    [SortOrder] int NOT NULL,
    [IsActive] bit NOT NULL,
    CONSTRAINT [PK_Brands] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [BusinessHours] (
    [Id] int NOT NULL IDENTITY,
    [DayOfWeek] int NOT NULL,
    [OpenTime] time NOT NULL,
    [CloseTime] time NOT NULL,
    [IsClosed] bit NOT NULL,
    CONSTRAINT [PK_BusinessHours] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [Categories] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(100) NOT NULL,
    [Slug] nvarchar(120) NOT NULL,
    [ParentId] int NULL,
    [Description] nvarchar(500) NULL,
    [ImageUrl] nvarchar(300) NULL,
    [Icon] nvarchar(10) NULL,
    [SortOrder] int NOT NULL,
    [ShowOnHome] bit NOT NULL,
    [IsActive] bit NOT NULL,
    [MetaTitle] nvarchar(200) NULL,
    [MetaDescription] nvarchar(500) NULL,
    CONSTRAINT [PK_Categories] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Categories_Categories_ParentId] FOREIGN KEY ([ParentId]) REFERENCES [Categories] ([Id]) ON DELETE NO ACTION
);
GO


CREATE TABLE [CmsPages] (
    [Id] int NOT NULL IDENTITY,
    [Title] nvarchar(150) NOT NULL,
    [Slug] nvarchar(160) NOT NULL,
    [Content] nvarchar(max) NULL,
    [MetaTitle] nvarchar(200) NULL,
    [MetaDescription] nvarchar(500) NULL,
    [ShowInHeader] bit NOT NULL,
    [ShowInFooter] bit NOT NULL,
    [SortOrder] int NOT NULL,
    [IsActive] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    CONSTRAINT [PK_CmsPages] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [ContactMessages] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(120) NOT NULL,
    [Email] nvarchar(150) NOT NULL,
    [Phone] nvarchar(30) NULL,
    [Subject] nvarchar(200) NULL,
    [Message] nvarchar(3000) NOT NULL,
    [IsRead] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_ContactMessages] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [Coupons] (
    [Id] int NOT NULL IDENTITY,
    [Code] nvarchar(40) NOT NULL,
    [Description] nvarchar(200) NULL,
    [DiscountType] int NOT NULL,
    [Value] decimal(18,2) NOT NULL,
    [MinOrderAmount] decimal(18,2) NOT NULL,
    [MaxDiscount] decimal(18,2) NULL,
    [StartsAt] datetime2 NULL,
    [ExpiresAt] datetime2 NULL,
    [UsageLimit] int NULL,
    [UsedCount] int NOT NULL,
    [IsActive] bit NOT NULL,
    CONSTRAINT [PK_Coupons] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [Customers] (
    [Id] int NOT NULL IDENTITY,
    [FirstName] nvarchar(60) NOT NULL,
    [LastName] nvarchar(60) NULL,
    [Email] nvarchar(150) NOT NULL,
    [Phone] nvarchar(30) NULL,
    [PasswordHash] nvarchar(max) NOT NULL,
    [Address] nvarchar(250) NULL,
    [City] nvarchar(80) NULL,
    [PostalCode] nvarchar(20) NULL,
    [IsActive] bit NOT NULL,
    [ResetToken] nvarchar(100) NULL,
    [ResetTokenExpiresAt] datetime2 NULL,
    [CreatedAt] datetime2 NOT NULL,
    [LastLoginAt] datetime2 NULL,
    CONSTRAINT [PK_Customers] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [Holidays] (
    [Id] int NOT NULL IDENTITY,
    [Date] datetime2 NOT NULL,
    [Title] nvarchar(150) NOT NULL,
    [Message] nvarchar(300) NULL,
    [IsClosedAllDay] bit NOT NULL,
    [OpenTime] time NULL,
    [CloseTime] time NULL,
    CONSTRAINT [PK_Holidays] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [NewsletterSubscribers] (
    [Id] int NOT NULL IDENTITY,
    [Email] nvarchar(150) NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_NewsletterSubscribers] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [NotificationLogs] (
    [Id] int NOT NULL IDENTITY,
    [Channel] int NOT NULL,
    [Recipient] nvarchar(200) NULL,
    [Subject] nvarchar(300) NULL,
    [Body] nvarchar(max) NULL,
    [Success] bit NOT NULL,
    [Error] nvarchar(1000) NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_NotificationLogs] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [ProductAttributes] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(100) NOT NULL,
    [InputType] int NOT NULL,
    [IsRequired] bit NOT NULL,
    [SortOrder] int NOT NULL,
    CONSTRAINT [PK_ProductAttributes] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [SiteSettings] (
    [Id] int NOT NULL IDENTITY,
    [LogoUrl] nvarchar(300) NULL,
    [FooterLogoUrl] nvarchar(300) NULL,
    [FaviconUrl] nvarchar(300) NULL,
    [CompanyName] nvarchar(150) NOT NULL,
    [Tagline] nvarchar(200) NULL,
    [Address] nvarchar(300) NULL,
    [Phone] nvarchar(40) NULL,
    [Email] nvarchar(150) NULL,
    [AboutText] nvarchar(1000) NULL,
    [MapEmbedUrl] nvarchar(500) NULL,
    [FooterText] nvarchar(300) NULL,
    [CurrencySymbol] nvarchar(10) NOT NULL,
    [CurrencyCode] nvarchar(10) NOT NULL,
    [TaxPercent] decimal(5,2) NOT NULL,
    [DeliveryCharge] decimal(18,2) NOT NULL,
    [FreeDeliveryAbove] decimal(18,2) NOT NULL,
    [MinOrderAmount] decimal(18,2) NOT NULL,
    [EnableDelivery] bit NOT NULL,
    [EnablePickup] bit NOT NULL,
    [EnableGuestCheckout] bit NOT NULL,
    [EnableTips] bit NOT NULL,
    [TimeZoneId] nvarchar(80) NOT NULL,
    [MetaTitle] nvarchar(200) NULL,
    [MetaDescription] nvarchar(500) NULL,
    [MetaKeywords] nvarchar(500) NULL,
    [HeadScripts] nvarchar(2000) NULL,
    [FacebookUrl] nvarchar(300) NULL,
    [InstagramUrl] nvarchar(300) NULL,
    [TwitterUrl] nvarchar(300) NULL,
    [YoutubeUrl] nvarchar(300) NULL,
    [LinkedinUrl] nvarchar(300) NULL,
    [TiktokUrl] nvarchar(300) NULL,
    [WhatsappNumber] nvarchar(40) NULL,
    [EnableCashOnDelivery] bit NOT NULL,
    [EnableCreditCard] bit NOT NULL,
    [EnablePaypal] bit NOT NULL,
    [PaymentTestMode] bit NOT NULL,
    [PaypalClientId] nvarchar(200) NULL,
    [PaypalSecret] nvarchar(200) NULL,
    [PaypalSandbox] bit NOT NULL,
    [CloverEnabled] bit NOT NULL,
    [CloverSandbox] bit NOT NULL,
    [CloverMerchantId] nvarchar(100) NULL,
    [CloverApiToken] nvarchar(200) NULL,
    [CloverEcommercePublicKey] nvarchar(200) NULL,
    [CloverEcommercePrivateKey] nvarchar(200) NULL,
    [CloverPrintToKitchen] bit NOT NULL,
    [SmsEnabled] bit NOT NULL,
    [TwilioAccountSid] nvarchar(100) NULL,
    [TwilioAuthToken] nvarchar(100) NULL,
    [TwilioFromNumber] nvarchar(30) NULL,
    [AdminAlertPhone] nvarchar(30) NULL,
    [EmailEnabled] bit NOT NULL,
    [SmtpHost] nvarchar(150) NULL,
    [SmtpPort] int NOT NULL,
    [SmtpUseSsl] bit NOT NULL,
    [SmtpUser] nvarchar(150) NULL,
    [SmtpPassword] nvarchar(150) NULL,
    [SmtpFromEmail] nvarchar(150) NULL,
    [AdminAlertEmail] nvarchar(150) NULL,
    [FrontSiteUrl] nvarchar(300) NULL,
    [AnnouncementText] nvarchar(300) NULL,
    CONSTRAINT [PK_SiteSettings] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [Tags] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(60) NOT NULL,
    [Slug] nvarchar(80) NOT NULL,
    [Subtitle] nvarchar(200) NULL,
    [BadgeColor] nvarchar(20) NOT NULL,
    [ShowOnHome] bit NOT NULL,
    [HomeProductCount] int NOT NULL,
    [SortOrder] int NOT NULL,
    [IsActive] bit NOT NULL,
    CONSTRAINT [PK_Tags] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [Products] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(200) NOT NULL,
    [Slug] nvarchar(220) NOT NULL,
    [Sku] nvarchar(50) NULL,
    [ShortDescription] nvarchar(500) NULL,
    [Description] nvarchar(max) NULL,
    [Price] decimal(18,2) NOT NULL,
    [SalePrice] decimal(18,2) NULL,
    [CategoryId] int NOT NULL,
    [BrandId] int NULL,
    [TrackStock] bit NOT NULL,
    [StockQuantity] int NOT NULL,
    [IsVegetarian] bit NOT NULL,
    [IsSpicy] bit NOT NULL,
    [PrepTimeMinutes] int NOT NULL,
    [IsActive] bit NOT NULL,
    [SortOrder] int NOT NULL,
    [ViewCount] int NOT NULL,
    [SoldCount] int NOT NULL,
    [MetaTitle] nvarchar(200) NULL,
    [MetaDescription] nvarchar(500) NULL,
    [CloverItemId] nvarchar(100) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    CONSTRAINT [PK_Products] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Products_Brands_BrandId] FOREIGN KEY ([BrandId]) REFERENCES [Brands] ([Id]) ON DELETE SET NULL,
    CONSTRAINT [FK_Products_Categories_CategoryId] FOREIGN KEY ([CategoryId]) REFERENCES [Categories] ([Id]) ON DELETE NO ACTION
);
GO


CREATE TABLE [Orders] (
    [Id] int NOT NULL IDENTITY,
    [OrderNumber] nvarchar(30) NOT NULL,
    [AccessKey] nvarchar(40) NOT NULL,
    [CustomerId] int NULL,
    [IsGuest] bit NOT NULL,
    [CustomerName] nvarchar(120) NOT NULL,
    [Email] nvarchar(150) NOT NULL,
    [Phone] nvarchar(30) NOT NULL,
    [Address] nvarchar(250) NULL,
    [City] nvarchar(80) NULL,
    [PostalCode] nvarchar(20) NULL,
    [Notes] nvarchar(500) NULL,
    [OrderType] int NOT NULL,
    [ScheduledFor] datetime2 NULL,
    [SubTotal] decimal(18,2) NOT NULL,
    [Discount] decimal(18,2) NOT NULL,
    [CouponCode] nvarchar(40) NULL,
    [Tax] decimal(18,2) NOT NULL,
    [DeliveryCharge] decimal(18,2) NOT NULL,
    [Tip] decimal(18,2) NOT NULL,
    [Total] decimal(18,2) NOT NULL,
    [PaymentMethod] int NOT NULL,
    [PaymentStatus] int NOT NULL,
    [TransactionId] nvarchar(150) NULL,
    [Status] int NOT NULL,
    [CloverOrderId] nvarchar(100) NULL,
    [CloverSyncError] nvarchar(500) NULL,
    [KitchenAcknowledged] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    CONSTRAINT [PK_Orders] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Orders_Customers_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [Customers] ([Id]) ON DELETE SET NULL
);
GO


CREATE TABLE [AttributeValues] (
    [Id] int NOT NULL IDENTITY,
    [AttributeId] int NOT NULL,
    [Value] nvarchar(100) NOT NULL,
    [SortOrder] int NOT NULL,
    CONSTRAINT [PK_AttributeValues] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AttributeValues_ProductAttributes_AttributeId] FOREIGN KEY ([AttributeId]) REFERENCES [ProductAttributes] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [ProductImages] (
    [Id] int NOT NULL IDENTITY,
    [ProductId] int NOT NULL,
    [Url] nvarchar(300) NOT NULL,
    [SortOrder] int NOT NULL,
    CONSTRAINT [PK_ProductImages] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ProductImages_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [ProductSpecifications] (
    [Id] int NOT NULL IDENTITY,
    [ProductId] int NOT NULL,
    [Name] nvarchar(100) NOT NULL,
    [Value] nvarchar(300) NOT NULL,
    [SortOrder] int NOT NULL,
    CONSTRAINT [PK_ProductSpecifications] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ProductSpecifications_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [ProductTags] (
    [ProductId] int NOT NULL,
    [TagId] int NOT NULL,
    CONSTRAINT [PK_ProductTags] PRIMARY KEY ([ProductId], [TagId]),
    CONSTRAINT [FK_ProductTags_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_ProductTags_Tags_TagId] FOREIGN KEY ([TagId]) REFERENCES [Tags] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [OrderItems] (
    [Id] int NOT NULL IDENTITY,
    [OrderId] int NOT NULL,
    [ProductId] int NULL,
    [ProductName] nvarchar(200) NOT NULL,
    [ImageUrl] nvarchar(300) NULL,
    [OptionsText] nvarchar(500) NULL,
    [UnitPrice] decimal(18,2) NOT NULL,
    [Quantity] int NOT NULL,
    [LineTotal] decimal(18,2) NOT NULL,
    CONSTRAINT [PK_OrderItems] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_OrderItems_Orders_OrderId] FOREIGN KEY ([OrderId]) REFERENCES [Orders] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_OrderItems_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id]) ON DELETE SET NULL
);
GO


CREATE TABLE [OrderStatusHistories] (
    [Id] int NOT NULL IDENTITY,
    [OrderId] int NOT NULL,
    [Status] int NOT NULL,
    [Note] nvarchar(500) NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_OrderStatusHistories] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_OrderStatusHistories_Orders_OrderId] FOREIGN KEY ([OrderId]) REFERENCES [Orders] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [ProductOptions] (
    [Id] int NOT NULL IDENTITY,
    [ProductId] int NOT NULL,
    [AttributeValueId] int NOT NULL,
    [PriceAdjustment] decimal(18,2) NOT NULL,
    [IsDefault] bit NOT NULL,
    CONSTRAINT [PK_ProductOptions] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ProductOptions_AttributeValues_AttributeValueId] FOREIGN KEY ([AttributeValueId]) REFERENCES [AttributeValues] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_ProductOptions_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id]) ON DELETE CASCADE
);
GO


CREATE UNIQUE INDEX [IX_AdminUsers_Username] ON [AdminUsers] ([Username]);
GO


CREATE INDEX [IX_AttributeValues_AttributeId] ON [AttributeValues] ([AttributeId]);
GO


CREATE UNIQUE INDEX [IX_Brands_Slug] ON [Brands] ([Slug]);
GO


CREATE INDEX [IX_Categories_ParentId] ON [Categories] ([ParentId]);
GO


CREATE UNIQUE INDEX [IX_Categories_Slug] ON [Categories] ([Slug]);
GO


CREATE UNIQUE INDEX [IX_CmsPages_Slug] ON [CmsPages] ([Slug]);
GO


CREATE UNIQUE INDEX [IX_Coupons_Code] ON [Coupons] ([Code]);
GO


CREATE UNIQUE INDEX [IX_Customers_Email] ON [Customers] ([Email]);
GO


CREATE UNIQUE INDEX [IX_NewsletterSubscribers_Email] ON [NewsletterSubscribers] ([Email]);
GO


CREATE INDEX [IX_OrderItems_OrderId] ON [OrderItems] ([OrderId]);
GO


CREATE INDEX [IX_OrderItems_ProductId] ON [OrderItems] ([ProductId]);
GO


CREATE INDEX [IX_Orders_CreatedAt] ON [Orders] ([CreatedAt]);
GO


CREATE INDEX [IX_Orders_CustomerId] ON [Orders] ([CustomerId]);
GO


CREATE UNIQUE INDEX [IX_Orders_OrderNumber] ON [Orders] ([OrderNumber]);
GO


CREATE INDEX [IX_OrderStatusHistories_OrderId] ON [OrderStatusHistories] ([OrderId]);
GO


CREATE INDEX [IX_ProductImages_ProductId] ON [ProductImages] ([ProductId]);
GO


CREATE INDEX [IX_ProductOptions_AttributeValueId] ON [ProductOptions] ([AttributeValueId]);
GO


CREATE INDEX [IX_ProductOptions_ProductId] ON [ProductOptions] ([ProductId]);
GO


CREATE INDEX [IX_Products_BrandId] ON [Products] ([BrandId]);
GO


CREATE INDEX [IX_Products_CategoryId] ON [Products] ([CategoryId]);
GO


CREATE UNIQUE INDEX [IX_Products_Slug] ON [Products] ([Slug]);
GO


CREATE INDEX [IX_ProductSpecifications_ProductId] ON [ProductSpecifications] ([ProductId]);
GO


CREATE INDEX [IX_ProductTags_TagId] ON [ProductTags] ([TagId]);
GO


CREATE UNIQUE INDEX [IX_Tags_Slug] ON [Tags] ([Slug]);
GO


