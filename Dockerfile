FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine

WORKDIR /app

ADD ./WDMRC.Console/bin/Release/net8.0/* /app

RUN apk add --no-cache icu-libs krb5-libs libgcc libintl libssl3 libstdc++ zlib
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

ENTRYPOINT ["dotnet","wdmrc.dll","-h","http://*"]

CMD ["-p","80"]

EXPOSE 80