﻿using ClothResorting.Helpers;
using ClothResorting.Models;
using ClothResorting.Models.FBAModels;
using System;
using System.Collections.Generic;
using System.Data.Entity.Migrations;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Data.Entity;
using ClothResorting.Helpers.FBAHelper;
using ClothResorting.Models.FBAModels.StaticModels;
using ClothResorting.Models.StaticClass;
using Microsoft.AspNet.Identity.EntityFramework;

namespace ClothResorting.Controllers
{
    [AllowAnonymous]
    public class HomeController : Controller
    {
        private ApplicationDbContext _context;

        public HomeController()
        {
            _context = new ApplicationDbContext();
        }

        public ActionResult Index()
        {
            if (User.IsInRole(RoleName.CanOperateAsT3) || User.IsInRole(RoleName.CanOperateAsT4) || User.IsInRole(RoleName.CanOperateAsT5) || User.IsInRole(RoleName.CanDeleteEverything))
                return View();
            else if (User.IsInRole(RoleName.CanOperateAsT2))
                return View("~/Views/Warehouse/Index.cshtml");
            else if (User.IsInRole(RoleName.CanViewAsClientOnly))
                return View("FBAClientIndex");
            else
                return RedirectToAction("Denied", "Home");
        }

        public ActionResult Test()
        {
            //var palletsLocation = _context.FBAPalletLocations
            //    .Include(x => x.FBAMasterOrder.Customer)
            //    .Where(x => x.FBAMasterOrder.Customer.CustomerCode == "NK" && x.AvailablePlts != 0)
            //    .ToList();

            var pickDetail = _context.FBAPickDetails
                .Where(x => x.ActualPlts != 0 && x.NewPlts == 0 && x.PltsFromInventory == 0)
                .ToList();

            ViewBag.Message = "Your application description page.";

            return View();
        }

        public ActionResult Contact()
        {
            ViewBag.Message = "Your contact page.";

            return View();
        }

        public ActionResult Denied()
        {
            return View();
        }

        private string UnifyTime(string dateString)
        {
            var dateArray = dateString.Split('/');
            var MM = "0";
            var dd = "0";
            var yyyy = "20";

            if (dateArray[0].Length == 1)
            {
                MM = MM + dateArray[0].ToString();
            }
            else
            {
                MM = dateArray[0].ToString();
            }

            if (dateArray[1].Length == 1)
            {
                dd = dd + dateArray[1].ToString();
            }
            else
            {
                dd = dateArray[1].ToString();
            }

            if (dateArray[2].Length == 2)
            {
                yyyy = yyyy + dateArray[2].ToString();
            }
            else if (dateArray[2].Length != 4)
            {
                yyyy = yyyy + dateArray[2][0].ToString() + dateArray[2][1].ToString();
            }
            else
            {
                yyyy = dateArray[2].ToString();
            }

            return yyyy + "-" + MM + "-" + dd;
        }
    }
}