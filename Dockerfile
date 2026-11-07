# One container: customer site (/) + admin panel (/admin) behind nginx, sharing one SQLite DB and uploads folder.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish "Food Booking Application/Food Booking Application.csproj" -c Release -o /out/site \
 && dotnet publish "FoodBooking.Admin/FoodBooking.Admin.csproj" -c Release -o /out/admin

FROM mcr.microsoft.com/dotnet/aspnet:10.0
RUN apt-get update && apt-get install -y --no-install-recommends nginx curl && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /out .
COPY deploy/nginx.conf /etc/nginx/nginx.conf
COPY deploy/start.sh /app/start.sh
RUN chmod +x /app/start.sh && mkdir -p /data/uploads

ENV ASPNETCORE_FORWARDEDHEADERS_ENABLED=true \
    DOTNET_gcServer=0 \
    Database__Provider=Sqlite \
    ConnectionStrings__Sqlite="Data Source=/data/foodbooking.db" \
    Uploads__PhysicalPath=/data/uploads
EXPOSE 8080
CMD ["/app/start.sh"]
