﻿using ClothResorting.Controllers.Api.Filters;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Http;
using System.Web.Http.Cors;

namespace ClothResorting
{
    public static class WebApiConfig
    {
        public static void Register(HttpConfiguration config)
        {
            // Web API configuration and services

            // Web API routes
            var settings = config.Formatters.JsonFormatter.SerializerSettings;    //取得进行序列化设置的对象
            settings.ContractResolver = new CamelCasePropertyNamesContractResolver();   //将ContractResolver设置为Camel解析器
            settings.Formatting = Formatting.Indented;    //排版缩进

            var cors = new EnableCorsAttribute("*", "*", "*");
            config.EnableCors(cors);

            //config.Filters.Add(new ValidateModelAttribute());

            config.MapHttpAttributeRoutes();

            config.Routes.MapHttpRoute(
                name: "FbaApi",
                routeTemplate: "api/Fba/{controller}/{id}",
                defaults: new { id = RouteParameter.Optional }
            );

            config.Routes.MapHttpRoute(
                name: "DefaultApi",
                routeTemplate: "api/{controller}/{id}",
                defaults: new { id = RouteParameter.Optional }
            );
        }
    }
}
