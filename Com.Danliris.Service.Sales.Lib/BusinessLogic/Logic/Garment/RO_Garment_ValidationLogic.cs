﻿using Com.Danliris.Service.Sales.Lib.Services;
using Com.Danliris.Service.Sales.Lib.Utilities;
using Com.Danliris.Service.Sales.Lib.Utilities.BaseClass;
using System;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Com.Danliris.Service.Sales.Lib.ViewModels.IntegrationViewModel.GarmentPurchaseRequestViewModel;
using Com.Danliris.Service.Sales.Lib.ViewModels.IntegrationViewModel;
using System.Linq;
using System.Net;
using Newtonsoft.Json;
using Com.Danliris.Service.Sales.Lib.Helpers;
using System.Threading.Tasks;
using Com.Danliris.Service.Sales.Lib.Models.CostCalculationGarments;

namespace Com.Danliris.Service.Sales.Lib.BusinessLogic.Logic.Garment
{
    public class RO_Garment_ValidationLogic
    {
        private HttpClient httpClient;
        private IIdentityService IdentityService;

        private string GarmentPurchaseRequestUri = "garment-purchase-requests";

        public RO_Garment_ValidationLogic(IServiceProvider serviceProvider)
        {
            IdentityService = serviceProvider.GetService<IIdentityService>();
        }

        private void SetHttpClient()
        {
            httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(JwtBearerDefaults.AuthenticationScheme, IdentityService.Token);
        }

        public async Task CreateGarmentPurchaseRequest(CostCalculationGarment costCalculationGarment, Dictionary<long, string> productDicts)
        {
            SetHttpClient();

            var stringContent = JsonConvert.SerializeObject(FillGarmentPurchaseRequest(costCalculationGarment, productDicts));
            var httpContent = new StringContent(stringContent, Encoding.UTF8, General.JsonMediaType);

            var httpResponseMessage = await httpClient.PostAsync($@"{APIEndpoint.AzurePurchasing}{GarmentPurchaseRequestUri}", httpContent);

            CheckResponse(httpResponseMessage);
        }

        public async Task AddItemsGarmentPurchaseRequest(CostCalculationGarment costCalculationGarment, Dictionary<long, string> productDicts)
        {
            SetHttpClient();

            var oldGarmentPurchaseRequest = await GetGarmentPurchaseRequestByRONo(costCalculationGarment.RO_Number);
            var garmentPurchaseRequest = FillGarmentPurchaseRequest(costCalculationGarment, productDicts);
            //garmentPurchaseRequest.Id = oldGarmentPurchaseRequest.Id;

            foreach (var item in garmentPurchaseRequest.Items)
            {
                oldGarmentPurchaseRequest.Items.Add(item);
            }

            var stringContent = JsonConvert.SerializeObject(oldGarmentPurchaseRequest);
            var httpContent = new StringContent(stringContent, Encoding.UTF8, General.JsonMediaType);

            var httpResponseMessage = await httpClient.PutAsync($@"{APIEndpoint.AzurePurchasing}{GarmentPurchaseRequestUri}/{oldGarmentPurchaseRequest.Id}", httpContent);

            CheckResponse(httpResponseMessage);
        }

        private async Task<GarmentPurchaseRequestViewModel> GetGarmentPurchaseRequestByRONo(string roNo)
        {
            var httpResponseMessage = await httpClient.GetAsync($@"{APIEndpoint.AzurePurchasing}{GarmentPurchaseRequestUri}/by-rono/{roNo}");

            if (httpResponseMessage.StatusCode.Equals(HttpStatusCode.OK))
            {
                var result = httpResponseMessage.Content.ReadAsStringAsync().Result;
                Dictionary<string, object> resultDict = JsonConvert.DeserializeObject<Dictionary<string, object>>(result);

                var data = resultDict.SingleOrDefault(p => p.Key.Equals("data")).Value;

                GarmentPurchaseRequestViewModel garmentPurchaseRequestViewModel = JsonConvert.DeserializeObject<GarmentPurchaseRequestViewModel>(data.ToString());

                return garmentPurchaseRequestViewModel;
            }
            else
            {
                return null;
            }
        }

        private void CheckResponse(HttpResponseMessage httpResponseMessage)
        {
            if (!httpResponseMessage.StatusCode.Equals(HttpStatusCode.Created))
            {
                string result = httpResponseMessage.Content.ReadAsStringAsync().Result;
                Dictionary<string, object> resultDict = JsonConvert.DeserializeObject<Dictionary<string, object>>(result);

                if (httpResponseMessage.StatusCode.Equals(HttpStatusCode.BadRequest))
                {
                    var error = resultDict.SingleOrDefault(p => p.Key.Equals("error")).Value;
                    throw new ServiceValidationException(JsonConvert.SerializeObject(error), null, null);
                }
                else
                {
                    var message = resultDict.SingleOrDefault(p => p.Key.Equals("message")).Value;
                    throw new Exception(message != null ? message.ToString() : result);
                }
            }
        }

        private List<GarmentPurchaseRequestItemViewModel> FillGarmentPurchaseRequestItems(List<CostCalculationGarment_Material> costCalculationGarment_Materials, Dictionary<long, string> productDicts)
        {
            List<GarmentPurchaseRequestItemViewModel> GarmentPurchaseRequestItems = costCalculationGarment_Materials.Select(i => new GarmentPurchaseRequestItemViewModel
            {
                PO_SerialNumber = i.PO_SerialNumber,
                Product = new ProductViewModel
                {
                    Id = Convert.ToInt64(i.ProductId),
                    Code = i.ProductCode,
                    Name = productDicts.GetValueOrDefault(Convert.ToInt64(i.ProductId))
                },
                Quantity = i.BudgetQuantity,
                BudgetPrice = i.Price,
                Uom = new UomViewModel
                {
                    Id = Convert.ToInt64(i.UOMPriceId),
                    Unit = i.UOMPriceName
                },
                Category = new CategoryViewModel
                {
                    Id = Convert.ToInt64(i.CategoryId),
                    Name = i.CategoryName
                },
                ProductRemark = i.Description
            }).ToList();

            return GarmentPurchaseRequestItems;
        }

        private GarmentPurchaseRequestViewModel FillGarmentPurchaseRequest(CostCalculationGarment costCalculation, Dictionary<long, string> productDicts)
        {
            GarmentPurchaseRequestViewModel garmentPurchaseRequest = new GarmentPurchaseRequestViewModel
            {
                RONo = costCalculation.RO_Number,
                Buyer = new BuyerViewModel
                {
                    Id = Convert.ToInt64(costCalculation.BuyerBrandId),
                    Code = costCalculation.BuyerBrandCode,
                    Name = costCalculation.BuyerBrandName
                },
                Article = costCalculation.Article,
                Date = DateTimeOffset.Now,
                ShipmentDate = costCalculation.DeliveryDate,
                Unit = new UnitViewModel
                {
                    Id = costCalculation.UnitId,
                    Code = costCalculation.UnitCode,
                    Name = costCalculation.UnitName
                },
                Items = FillGarmentPurchaseRequestItems(costCalculation.CostCalculationGarment_Materials.ToList(), productDicts)
            };
            return garmentPurchaseRequest;
        }
    }
}