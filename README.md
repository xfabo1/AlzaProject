# AlzaBox Routing System

## Description:
This project plans routes for delivering goods from central warehouses to AlzaBoxes. These goods are transported by vans where each warehouse contains 120 of them and they do two trips per day. To simplify this work, I have made some assumptions.

I assume that the mobile app or web application will be the one responsible for calling the CREATE order on the backend with the location of the AlzaBox, which the customer picked. The location should be in latitude and longitude coordinates. Next is that the warehouses are only 1 per region and specifically in the capital of that region. This means there are a total of 13 warehouses considering that both Středočeský kraj and Hlavní město Praha have the same capital.

The next assumption is that the flow is that someone or something will trigger an action on the backend to generate all the routes for one specific day and one specific warehouse. This makes it so the planning can happen for all the warehouses separately.

Once the routes are planned for the specific day and warehouse, each route has its own route ID. This route ID is used to identify all the orders that should be delivered during that route. 

Since there can be so many order and we only have limited asmount of vans, I have used an algorithm that calculates how much the order is worth (its price) considering also how much space it takes. This is because there might be a really large orders that even though they cost a lot of money, they also take a lot of space. We would prefer smaller objects (like iPhones) because we can deliver a lot of these and they cost a lot of money.

## Technical Description

I have used PostgreSQL with an extension called PostGIS that will allow me to find the nearest warehouse for AlzaBox so I can match order with warehouse and know which warehouse should be used for the order. This is later used for calculating the routes.

The database has 3 tables:
- Orders
- Warehouses
- Routes

The columns of each table are defined by the entity classes in `src/Planner.Api/Data/Entities.cs` (`Warehouse`, `DeliveryRoute` and `Order`). The indexes are configured in `src/Planner.Api/Data/PlannerDbContext.cs`.

## Endpoints

### Orders
- `POST /orders`: creates an order from its weight, volume, price and the latitude and longitude of the AlzaBox. The nearest warehouse is found with PostGIS and stored with the order. Returns the new order ID and the warehouse.
- `GET /orders/{id}`: returns one order, including its warehouse and the route it was planned onto (empty if it was not planned yet).

### Warehouses
- `GET /warehouses`: lists all the warehouses with their coordinates and the number of their orders in total, already planned onto a route and still open.
- `GET /warehouses/{id}/routes?date=2026-10-20`: plans the routes of the warehouse for the given day, if that day was not planned yet, and returns the route IDs. The orders that were not planned yet are used and the ones that do not fit stay open for the next day. Calling it again for the same day plans nothing new and returns the same IDs.

### Routes
- `GET /routes?warehouseId=1&date=2026-10-20&onlyFree=true`: returns only the IDs of the planned routes of the warehouse for the day. By default only the routes that no driver has picked yet, `onlyFree=false` returns all of them.
- `GET /routes/{id}`: returns one route with all the orders that should be delivered during it.
- `POST /routes/{id}/assign`: a driver picks the route for a van. The first request gets `204`, anybody who asks later for the same route gets `409`, and an unknown route returns `404`.


## How to test

### 1. Start the application
Docker is required. In the root directory (where `docker-compose.yml` is) run:

```
docker compose up --build
```

This starts the database (PostgreSQL with PostGIS) and the API on `http://localhost:8080`. On the first start the application creates the tables and seeds the database with 1 million random orders, which takes a while. The number can be changed with `Seed__Orders` in `docker-compose.yml` (`0` disables the seed).

The tables are created when the application starts and there are no migrations. After any change of the schema, or to start again with fresh data, remove the database volume first:

```
docker compose down -v
docker compose up --build
```

### 2. Try the endpoints
Use Postman or `curl` (in PowerShell use `curl.exe`). The examples below use warehouse `1` (Praha) and the day `2026-10-20`.

1. `GET http://localhost:8080/warehouses`: shows all the warehouses with their `totalOrders`, `plannedOrders` and `openOrders`. At the beginning `plannedOrders` is 0 everywhere.
2. `GET http://localhost:8080/warehouses/1/routes?date=2026-10-20`: plans the day. `plannedNow` (meaning it was created after this call) is `true` and the response contains the route IDs (at most 240) and `stillPending`, the number of orders that did not fit.
3. Call the same URL again: `plannedNow` is `false` and the route IDs are the same, nothing is planned twice.
4. `GET http://localhost:8080/routes?warehouseId=1&date=2026-10-20`: the IDs of the routes that no driver has picked yet.
5. `GET http://localhost:8080/routes/{id}` with one of the IDs: the route with all its orders.
6. `POST http://localhost:8080/routes/{id}/assign`: the first call returns `204`, the second one `409`, and an ID that does not exist `404`.
7. `GET http://localhost:8080/warehouses` again: `plannedOrders` and `openOrders` of warehouse 1 changed.
8. `POST http://localhost:8080/orders` with the body (in Postman choose Body, raw, JSON):

```json
{
  "weightKg": 10,
  "volumeM3": 0.1,
  "priceCzk": 500,
  "lat": 50.08,
  "lon": 14.42
}
```

The response contains the ID of the new order and the warehouse it was assigned to (Praha for these coordinates). `GET http://localhost:8080/orders/{id}` returns it, and its `routeId` is empty until the warehouse is planned again for another day.
