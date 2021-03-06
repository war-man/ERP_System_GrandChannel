﻿using ClothResorting.Models;
using ClothResorting.Models.StaticClass;
using Microsoft.AspNet.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Data.Entity;
using System.Web.Script.Serialization;
using System.Runtime.Serialization.Json;
using System.IO;
using Jil;
using ClothResorting.Models.QBOModels;
using Newtonsoft.Json;
using System.Configuration;

namespace ClothResorting.Helpers
{
    public class QBOServiceManager
    {
        private ApplicationDbContext _context;
        private string _baseUrl;
        private string _userId;
        private OAuthInfo _oauthInfo;
        private string _userName;
        private DateTime _syncDate;

        public QBOServiceManager()
        {
            _syncDate = DateTime.Now;
            _context = new ApplicationDbContext();
            _userName = HttpContext.Current.User.Identity.Name.Split('@')[0] == "" ? (HttpContext.Current.Request.Headers.Get("AppUser") == null ? "" : HttpContext.Current.Request.Headers.Get("AppUser")) : HttpContext.Current.User.Identity.Name.Split('@')[0];
            _baseUrl = ConfigurationManager.AppSettings["baseUrl"];
            _userId = HttpContext.Current.User.Identity.GetUserId<string>();
            _oauthInfo = _context.Users
                .Include(x => x.OAuthInfo)
                .SingleOrDefault(x => x.Id == _userId)
                .OAuthInfo
                .SingleOrDefault(x => x.PlatformName == Platform.QBO);
        }

        //将系统中的收费服务项目与QBO中的收费服务对比，将系统中有但QBO中没有的项目同步到QBO中去
        #region Steps description
            //1.检查公司ERP系统中的收费项目是否与QBO中的收费项目同步，并同步未同步的部分
            //1.1 逐个检查invoice中的收费项目是否在QBO中存在，如不存在，则在QBO中新建这个项目
            //2.检查公司ERP系统中的Customer是否与QBO中的Customer同步，如已同步则查询对应Customer的Value，否则报错，显示要求手动同步
            //3.将要同步的Invoice对象转换成QBO能识别的对象并序列化
            //4.同步Invoice
        #endregion
        public InvoiceResult SyncInvoiceToQBO(int invoiceId)
        {
            var invoiceInDb = _context.Invoices
                .Include(x => x.InvoiceDetails)
                .Include(x => x.UpperVendor)
                .SingleOrDefault(x => x.Id == invoiceId);

            var companyName = invoiceInDb.UpperVendor.Name;

            var companyId = string.Empty;
            var accountValue = QueryAccountId(_oauthInfo);

            #region Step 1
            ////获取系统中所有不重名的收费项目列表(未解决)
            //var itemList = _context.ChargingItems.ToList();

            ////获取QBO中的收费列表
            //var itemQuery = "SELECT * FROM Item WHERE Type = 'Service'";
            //var itemJsonResponseData = WebServiceManager.SendQueryRequest(QBOUrlGenerator.QueryRequestUrl(_baseUrl, oauthInfo.RealmId, itemQuery), oauthInfo.AccessToken);

            ////将获得的Json对象反序列化
            //var itemResponseBody = new ItemResponseBody();
            //using (var input = new StringReader(itemJsonResponseData))
            //{
            //    //var responseBody = JSON.Deserialize<ResponseBody>(input);     //Jil的反序列化方法不太好用
            //    itemResponseBody = JsonConvert.DeserializeObject<ItemResponseBody>(itemJsonResponseData);
            //}

            ////对比两表，将不重复的item做成QBO能接受的对象格式并序列化
            //foreach (var item in itemList)
            //{
            //    if (itemResponseBody.QueryResponse.Item.Where(x => x.Name == item.Name).Count() == 0)
            //    {
            //        var itemCreateRequestModel = new ItemCreateRequestModel
            //        {
            //            IncomeAccountRef = new IncomeAccountRef { Value = "26" },    //默认关联账户是26 Services账户
            //            //IncomeAccountRef = new IncomeAccountRef { Value = "1" },    //默认关联账户是1 Services账户
            //            Name = item.Name,
            //            Type = "Service"
            //        };

            //        string itemJsonData = JsonConvert.SerializeObject(itemCreateRequestModel);
            //        //调用建立新Item的API在QBO中建立不重复的收费项目
            //        var itemResponse = WebServiceManager.SendCreateRequest(QBOUrlGenerator.CreateRequestUrl(_baseUrl, oauthInfo.RealmId, "item"), itemJsonData, "POST", oauthInfo.AccessToken);
            //    }
            //}

            //获取QBO中的收费列表
            var itemQuery = "SELECT * FROM Item WHERE Type = 'Service'";
            var itemJsonResponseData = WebServiceManager.SendQueryRequest(QBOUrlGenerator.QueryRequestUrl(_baseUrl, _oauthInfo.RealmId, itemQuery), _oauthInfo.AccessToken);

            //将获得的Json对象反序列化
            var itemResponseBody = new ItemResponseBody();
            using (var input = new StringReader(itemJsonResponseData))
            {
                //var responseBody = JSON.Deserialize<ResponseBody>(input);     //Jil的反序列化方法不太好用
                itemResponseBody = JsonConvert.DeserializeObject<ItemResponseBody>(itemJsonResponseData);
            }

            var itemsInQbo = itemResponseBody.QueryResponse.Item.ToList();

            //逐个检查该invoice下的每一个收费项目名称是否在QBO中存在
            foreach (var i in invoiceInDb.InvoiceDetails)
            {
                var itemName = i.Activity;

                //如果不存在则新建一个，默认放在一个income账户下，该账户的Id由用户提供
                if (itemsInQbo.SingleOrDefault(x => x.Name == itemName) == null)
                {
                    var itemCreateRequestModel = new ItemCreateRequestModel
                    {
                        IncomeAccountRef = new IncomeAccountRef { Value = accountValue },    //默认关联账户是26 Services账户
                        Name = itemName,
                        Type = "Service"
                    };

                    string itemJsonData = JsonConvert.SerializeObject(itemCreateRequestModel);
                    //调用建立新Item的API在QBO中建立不重复的收费项目
                    var itemResponse = WebServiceManager.SendCreateRequest(QBOUrlGenerator.CreateRequestUrl(_baseUrl, _oauthInfo.RealmId, "item"), itemJsonData, "POST", _oauthInfo.AccessToken);
                }
            }

            #endregion

            #region Step 2 检查公司ERP系统中的Customer是否与QBO中的Customer同步，如已同步则查询对应Customer的Value，否则报错，显示要求手动同步
            var customerQuery = "SELECT * FROM CUSTOMER WHERE COMPANYNAME = '" + companyName + "'";

            var companyNameJsonResponseData = WebServiceManager.SendQueryRequest(QBOUrlGenerator.QueryRequestUrl(_baseUrl, _oauthInfo.RealmId, customerQuery), _oauthInfo.AccessToken);

            var customerResponseBody = new CustomerResponseBody();
            using (var input = new StringReader(itemJsonResponseData))
            {
                customerResponseBody = JsonConvert.DeserializeObject<CustomerResponseBody>(companyNameJsonResponseData);
            }

            //如果响应表示QBO中没有我们要查询的公司，则报错，提示手动在QBO中建立
            if (customerResponseBody.QueryResponse.MaxResults == 0)
            {
                throw new Exception("Company " + companyName + " was not found in QuickBooks. Please create that company in QuickBooks first and try again.");
            }
            else
            {
                companyId = customerResponseBody.QueryResponse.Customer.First().Id;
            }
            #endregion

            #region Step 3 将要同步的Invoice对象转换成QBO能识别的对象并序列化，并同步
            var invoice = new InvoiceRequestBody {
                CustomerRef = new CustomerRef(),
            };
            var lineList = new List<Line>();

            //查询目标Invoice中的收费项目分别在QBO中的Id(value)
            //再次查询收费项目列表并将响应反序列化
            itemJsonResponseData = WebServiceManager.SendQueryRequest(QBOUrlGenerator.QueryRequestUrl(_baseUrl, _oauthInfo.RealmId, itemQuery), _oauthInfo.AccessToken);
            itemResponseBody = new ItemResponseBody();
            using (var input = new StringReader(itemJsonResponseData))
            {
                itemResponseBody = JsonConvert.DeserializeObject<ItemResponseBody>(itemJsonResponseData);
            }

            if (invoiceInDb.InvoiceDetails.Count == 0)
            {
                throw new Exception("Upload failed because the invoice is empty.");
            }

            //生成QBO能接受的invoice格式
            foreach (var i in invoiceInDb.InvoiceDetails)
            {
                var line = new Line {
                    Amount = i.Rate * i.Quantity,
                    DetailType = "SalesItemLineDetail",
                    SalesItemLineDetail = new SalesItemLineDetail {
                        ItemRef = new ItemRef { Value = itemResponseBody.QueryResponse.Item.SingleOrDefault(x => x.Name == i.Activity).Id },
                        UnitPrice = i.Rate,
                        Qty = i.Quantity
                    }
                };

                lineList.Add(line);
            }

            invoice.Line = lineList;
            invoice.CustomerRef.Value = companyId;

            var invoiceJsonData = JsonConvert.SerializeObject(invoice);
            var invoiceJsonResponseData = WebServiceManager.SendCreateRequest(QBOUrlGenerator.CreateRequestUrl(_baseUrl, _oauthInfo.RealmId, "invoice"), invoiceJsonData, "POST", _oauthInfo.AccessToken);

            //将返回的数据继续与数据库的invoice数据同步
            var invoiceResult = new InvoiceResult();

            using (var input = new StringReader(invoiceJsonResponseData))
            {
                invoiceResult = JsonConvert.DeserializeObject<InvoiceResult>(invoiceJsonResponseData);
            }

            return invoiceResult;
            #endregion
        }

        public void SyncInvoiceFromQBO(string vendor)
        {
            var vendorInDb = _context.UpperVendors.SingleOrDefault(x => x.Name == vendor);

            var invoicesInDb = _context.Invoices
                .Include(x => x.UpperVendor)
                .Include(x => x.InvoiceDetails)
                .Where(x => x.UpperVendor.Name == vendor);

            var existedInvoiceList = new List<QBOInvoice>();
            var newInvoiceList = new List<QBOInvoice>();

            var invoiceQueryResult = QueryAllInvoices(_oauthInfo);

            var filteredInvoices = invoiceQueryResult.QueryResponse.Invoice.Where(x => x.CustomerRef.Name == vendor);

            //找出系统中之前同步过且存在的invoice（doc号相同）
            //找出新invoice（系统中没有的invoice）
            foreach (var i in filteredInvoices)
            {
                if (!invoicesInDb.Select(x => x.InvoiceNumber).Contains(i.DocNumber))
                {
                    newInvoiceList.Add(i);
                }
                else
                {
                    existedInvoiceList.Add(i);
                }
            }

            //同步旧invoice
            foreach(var i in existedInvoiceList)
            {
                var invoiceInDb = invoicesInDb.SingleOrDefault(x => x.InvoiceNumber == i.DocNumber);

                invoiceInDb.InvoiceDate = i.TxnDate.ToString("yyyy-MM-dd");
                invoiceInDb.DueDate = i.DueDate.ToString("yyyy-MM-dd");
                invoiceInDb.ShipDate = i.ShipDate.ToString("yyyy-MM-dd");
                invoiceInDb.CreatedDate = i.MetaData.CreateTime;
                invoiceInDb.UploadedDate = _syncDate;
                invoiceInDb.UploadedBy = _userName;

                //如果totalamt不一样或line数量不同，说明invoice内容改过了，需要删掉旧内容，重新同步新内容
                if (i.TotalAmt != invoiceInDb.TotalDue || (i.Line.Count - 1) != invoiceInDb.InvoiceDetails.Count)
                {
                    _context.InvoiceDetails.RemoveRange(invoiceInDb.InvoiceDetails);

                    invoiceInDb.InvoiceDetails = null;

                    i.Line.Remove(i.Line[i.Line.Count - 1]);

                    for (var j = 0; j < i.Line.Count; j++)
                    {
                        var newLine = new InvoiceDetail
                        {
                            Activity = i.Line[j].SalesItemLineDetail == null ? "NA" : i.Line[j].SalesItemLineDetail.ItemRef.Name,
                            ChargingType = i.Line[j].ItemAccountRef == null ? "NA" : i.Line[j].ItemAccountRef.Name,
                            Unit = "NA",
                            Quantity = i.Line[j].SalesItemLineDetail == null ? 0 : i.Line[j].SalesItemLineDetail.Qty,
                            Rate = i.Line[j].SalesItemLineDetail == null ? 0 : i.Line[j].SalesItemLineDetail.UnitPrice,
                            Amount = i.Line[j].Amount,
                            Memo = i.Line[j].Description == null ? "NA" : i.Line[j].Description,
                            Invoice = invoiceInDb
                        };

                        _context.InvoiceDetails.Add(newLine);
                    }
                }

                invoiceInDb.TotalDue = i.TotalAmt;
            }

            ////同步新invoice
            //if (newInvoiceList.Count == 0)
            //{
            //    throw new Exception("No more new invoices need to be synchronized.");
            //}

            foreach(var i in newInvoiceList)
            {
                var newInvoice = new Models.Invoice {
                    InvoiceNumber = i.DocNumber,
                    PurchaseOrder = "INSIDE",
                    Container = "INSIDE",
                    InvoiceDate = i.TxnDate.ToString("yyyy-MM-dd"),
                    TotalDue = i.TotalAmt,
                    DueDate = i.DueDate.ToString("yyyy-MM-dd"),
                    Enclosed = "NA",
                    ShipDate = i.ShipDate.ToString("yyyy-MM-dd"),
                    ShipVia = "NA",
                    CreatedDate = i.MetaData.CreateTime,
                    CreatedBy = "FROM QBO",
                    UploadedDate = _syncDate,
                    UploadedBy = _userName,
                    UpperVendor = vendorInDb,
                    RequestId = "First sync at " + _syncDate.ToString("yyy--MM-dd")
                };

                _context.Invoices.Add(newInvoice);

                i.Line.Remove(i.Line[i.Line.Count - 1]);

                for (var j = 0; j < i.Line.Count; j++)
                {
                    var newLine = new InvoiceDetail
                    {
                        Activity = i.Line[j].SalesItemLineDetail == null ? "NA" : i.Line[j].SalesItemLineDetail.ItemRef.Name,
                        ChargingType = i.Line[j].ItemAccountRef == null ? "NA" : i.Line[j].ItemAccountRef.Name,
                        Unit = "NA",
                        Quantity = i.Line[j].SalesItemLineDetail == null ? 0 : i.Line[j].SalesItemLineDetail.Qty,
                        Rate = i.Line[j].SalesItemLineDetail == null ? 0 : i.Line[j].SalesItemLineDetail.UnitPrice,
                        Amount = i.Line[j].Amount,
                        Memo = i.Line[j].Description == null ? "NA" : i.Line[j].Description,
                        Invoice = newInvoice
                    };

                    _context.InvoiceDetails.Add(newLine);
                }
            }

            _context.SaveChanges();
        }

        InvoiceResponseBody QueryAllInvoices(OAuthInfo oauthInfo)
        {
            var inoviceResponseBody = new InvoiceResponseBody();

            //获取QBO中所有的Invoice
            var invoiceQuery = "SELECT * FROM Invoice";
            var invoiceJsonResponseData = WebServiceManager.SendQueryRequest(QBOUrlGenerator.QueryRequestUrl(_baseUrl, oauthInfo.RealmId, invoiceQuery), oauthInfo.AccessToken);

            using (var input = new StringReader(invoiceJsonResponseData))
            {
                inoviceResponseBody = JsonConvert.DeserializeObject<InvoiceResponseBody>(invoiceJsonResponseData);
            }

            return inoviceResponseBody;
        }

        string QueryAccountId(OAuthInfo oauthInfo)
        {
            var accountName = ConfigurationManager.AppSettings["accountName"];
            //var accountType = ConfigurationManager.AppSettings["accountType"];
            //var accountSubType = ConfigurationManager.AppSettings["accountSubType"];

            var accountType = "Income";
            var accountSubType = "ServiceFeeIncome";

            var accountQuery = "select * from Account where Name = '" + accountName + "' AND AccountType='" + accountType + "' AND AccountSubType='" + accountSubType + "'";
            var accountJsonResponseData = WebServiceManager.SendQueryRequest(QBOUrlGenerator.QueryRequestUrl(_baseUrl, oauthInfo.RealmId, accountQuery), oauthInfo.AccessToken);
            var accountResponseBody = new AccountResponse();
            using (var input = new StringReader(accountJsonResponseData))
            {
                accountResponseBody = JsonConvert.DeserializeObject<AccountResponse>(accountJsonResponseData);
            }

            if (accountResponseBody.QueryResponse.Account == null)
            {
                throw new Exception("Cannot find any account named " + accountName + " matching type " + accountType + " and subtype " + accountSubType + ".");
            }

            return accountResponseBody.QueryResponse.Account.First().Id;
        }
    }

    public class AccountResponse
    {
        public QueryResponse QueryResponse { get; set; }
    }

    public class QueryResponse
    {
        public List<Account> Account { get; set; }
    }

    public class Account
    {
        public string Id { get; set; }

        public string Name { get; set; }

        public string AccountType { get; set; }

        public string AccountSubType { get; set; }
    }

    public class Invoice
    {
        public string DocNumber { get; set; }

        public CustomerRef CustomerRef { get; set; }

        public IEnumerable<Line> Line { get; set; }

        public string Id { get; set; }

        public MetaData MetaData { get; set; }
    }

    public class InvoiceResult
    {
        public Invoice Invoice { get; set; }

        [JsonProperty("time")]
        public DateTime Time { get; set; }
    }
}