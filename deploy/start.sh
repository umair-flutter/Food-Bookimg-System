#!/bin/sh
# Starts the customer site, waits until it has created and seeded the database,
# then starts the admin panel under /admin and finally nginx on port 8080.
set -e
cd /app/site
ASPNETCORE_URLS=http://127.0.0.1:5000 dotnet "Food Booking Application.dll" &
i=0
until curl -fs -o /dev/null http://127.0.0.1:5000/ || [ $i -ge 120 ]; do i=$((i+1)); sleep 1; done
cd /app/admin
PathBase=/admin ASPNETCORE_URLS=http://127.0.0.1:5001 dotnet FoodBooking.Admin.dll &
exec nginx -g 'daemon off;'
