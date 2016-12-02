﻿using HelpDesk.DAL.Abstract;
using HelpDesk.DAL.Concrete;
using HelpDesk.DAL.Entities;
using HelpDesk.UI.Infrastructure;
using HelpDesk.UI.ViewModels;
using HelpDesk.UI.ViewModels.Settings;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.Owin;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Entity.Migrations;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;

namespace HelpDesk.UI.Controllers.MVC
{
    [Authorize]
    public class SettingsController : Controller
    {
        private readonly IUnitOfWork unitOfWork;
        private readonly IdentityHelper identityHelper;

        public SettingsController(IUnitOfWork unitOfWork)
        {
            this.unitOfWork = unitOfWork;
            this.identityHelper = new IdentityHelper();
        }        

        public ViewResult Index()
        {
            Settings settings = identityHelper.CurrentUser.Settings;

            IndexViewModel model = new IndexViewModel
            {
                NewTicketsNotifications = settings.NewTicketsNotifications,
                SolvedTicketsNotifications = settings.SolvedTicketsNotifications,                
                TicketsPerPage = settings.TicketsPerPage
            };

            if (identityHelper.IsCurrentUserAnAdministrator())
            {
                model.UsersPerPage = settings.UsersPerPage;
                model.Categories = unitOfWork.CategoryRepository.Get(orderBy: query => query.OrderBy(category => category.Order)).ToList();
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Index([Bind(Include = "NewTicketsNotifications,SolvedTicketsNotifications,UsersPerPage,TicketsPerPage,CategoriesId,CategoriesName")] IndexViewModel model)
        {
            if (!identityHelper.IsCurrentUserAnAdministrator())
            {
                ModelState.Remove("UsersPerPage");
                ModelState.Remove("CategoriesId");
                ModelState.Remove("CategoriesName");
            }
            else
            {
                model.Categories = new List<Category>();
                for (int i = 0; i < (model.CategoriesId?.Length ?? 0); i++)
                {
                    model.Categories.Add(new Category
                    {
                        CategoryId = model.CategoriesId[i],
                        Name = model.CategoriesName[i],
                        Order = i
                    });
                }
            }

            try
            {
                if (ModelState.IsValid)
                {
                    Settings settings = unitOfWork.SettingsRepository.GetById(identityHelper.CurrentUser.Id);
                    settings.NewTicketsNotifications = model.NewTicketsNotifications;
                    settings.SolvedTicketsNotifications = model.SolvedTicketsNotifications;
                    settings.TicketsPerPage = model.TicketsPerPage;

                    if (identityHelper.IsCurrentUserAnAdministrator())
                    {
                        settings.UsersPerPage = model.UsersPerPage;

                        IEnumerable<Category> existingCategories = unitOfWork.CategoryRepository.Get(includeProperties: "Tickets");

                        foreach (Category existingCategory in existingCategories)
                        {
                            if (model.Categories.SingleOrDefault(c => c.CategoryId == existingCategory.CategoryId) == null)
                                unitOfWork.CategoryRepository.Delete(existingCategory);
                        }

                        foreach (Category newCategory in model.Categories)
                        {
                            Category category = existingCategories.SingleOrDefault(c => c.CategoryId == newCategory.CategoryId);
                            if (category == null)
                                unitOfWork.CategoryRepository.Insert(new Category
                                {
                                    Name = newCategory.Name,
                                    Order = newCategory.Order
                                });
                            else
                            {
                                category.Name = newCategory.Name;
                                category.Order = newCategory.Order;
                                unitOfWork.CategoryRepository.Update(category);
                            }
                        }
                    }
                    unitOfWork.SettingsRepository.Update(settings);
                    unitOfWork.Save();
                    TempData["Success"] = "Successfully saved settings.";
                    return RedirectToAction("Index");
                }
            }
            catch
            {
                TempData["Fail"] = "Unable to save settings. Try again, and if the problem persists contact your system administrator.";
            }

            return View(model);
        }

        protected override void OnException(ExceptionContext filterContext)
        {
            if (filterContext.ExceptionHandled)
            {
                return;
            }
            filterContext.Result = new ViewResult
            {
                ViewName = "~/Views/Shared/Error.aspx"
            };
            filterContext.ExceptionHandled = true;
        }
    }    
}