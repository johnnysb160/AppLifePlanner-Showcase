# ---------- build ----------
FROM mcr.microsoft.com/dotnet/sdk:8.0-noble AS build
WORKDIR /src

# copia csproj primeiro (cache restore)
COPY AppControlOrganizer.V2.Web/AppControlOrganizer.V2.Web.csproj AppControlOrganizer.V2.Web/
COPY AppControlOrganizer.V2.Shared/AppControlOrganizer.V2.Shared.csproj AppControlOrganizer.V2.Shared/

RUN dotnet restore "AppControlOrganizer.V2.Web/AppControlOrganizer.V2.Web.csproj" -r linux-x64

# copia o resto
COPY . .

WORKDIR /src/AppControlOrganizer.V2.Web
RUN dotnet publish "AppControlOrganizer.V2.Web.csproj" \
  -c Release \
  -r linux-x64 \
  --self-contained false \
  -o /app/publish

# ---------- runtime ----------
FROM mcr.microsoft.com/dotnet/aspnet:8.0-noble AS final
WORKDIR /app
EXPOSE 8080

ENV ASPNETCORE_URLS=http://+:8080

# The app reads server local time for "today" and month boundaries, so the container runs in the users' zone.
ENV TZ=America/Sao_Paulo

# Bakes a monthly DB-IP Lite Country GeoIP database (free, CC BY 4.0, no account/key, ~8 MB
# uncompressed — the Country Lite edition is used instead of the ~127 MB City edition since only
# the country is stored) into the image for the Access Analytics feature, since the container has
# no persistent disk. Best-effort: a transient download failure must not break the build, since
# geo lookup already degrades to "unknown" when the file is absent.
RUN apt-get update \
  && apt-get install -y --no-install-recommends curl gzip tzdata \
  && rm -rf /var/lib/apt/lists/* \
  && mkdir -p /app/GeoIP \
  && (curl -fsSL "https://download.db-ip.com/free/dbip-country-lite-$(date +%Y-%m).mmdb.gz" -o /tmp/dbip-country-lite.mmdb.gz \
      && gunzip -c /tmp/dbip-country-lite.mmdb.gz > /app/GeoIP/dbip-country-lite.mmdb \
      && rm -f /tmp/dbip-country-lite.mmdb.gz \
      || echo "GeoIP download failed; continuing without a baked GeoIP database.")

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "AppControlOrganizer.V2.Web.dll"]
