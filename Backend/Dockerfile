FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY NetromEcoMeal.Web/NetromEcoMeal.Web.csproj NetromEcoMeal.Web/
COPY NetromEcoMeal.BusinessLogic/NetromEcoMeal.BusinessLogic.csproj NetromEcoMeal.BusinessLogic/
COPY NetromEcoMeal.DataAccess/NetromEcoMeal.DataAccess.csproj NetromEcoMeal.DataAccess/
RUN dotnet restore "NetromEcoMeal.Web/NetromEcoMeal.Web.csproj"
COPY . .
RUN dotnet publish "NetromEcoMeal.Web/NetromEcoMeal.Web.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "NetromEcoMeal.Web.dll"]
