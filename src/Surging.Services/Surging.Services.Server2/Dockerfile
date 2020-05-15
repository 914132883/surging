FROM mcr.microsoft.com/dotnet/core/runtime:3.1.0
WORKDIR /Surging.Services.Server2
Copy . /Surging.Services.Server2
ENV TZ=Asia/Shanghai 
RUN ln -snf /usr/share/zoneinfo/$TZ /etc/localtime && echo $TZ > /etc/timezone 
EXPOSE 2801

ENV Register_Conn=127.0.0.1:8500
ENV EventBusConnection=localhost
ENV Surging_Server_IP=0.0.0.0
ENV Mapping_ip=
ENV Mapping_port=
ENV RootPath=
ENTRYPOINT ["dotnet","Surging.Services.Server2.dll"]