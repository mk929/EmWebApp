﻿using EmWebApp.BLL;
using EmWebApp.Models;
using EmWebApp.Models.Data;
using EmWebApp.Util;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net.Mail;
using System.Net.Mime;
using System.Threading.Tasks;
using System.Web.Hosting;
using System.Web.Mvc;
using System.Web.Routing;

namespace EmWebApp.Controllers
{
    public class ConsularApptController : Controller
    {
        // GET: ConsularAppt/Create
        public ActionResult Create()
        {
            ViewBag.Title = "Consular Appointment Request - New";
            return View(new ConsularApptVM());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Create([Bind(Include = "AppointmentDate,AppointmentType,Name,Gender,DateOfBirth,PlaceOfBirth,Nationality,NRIC_No,PassportNumber,PassportIssuedDate,ConsulateLocation,StayType,StayPermitNumber,EmployerName,Occupation,ContactAddr1,ContactAddr2,ContactPhone,ContactEmail,HomeAddr1,HomeAddr2,HomePhone,Note")] ConsularApptVM consularApptVM)
        {
            EmbassyAppDb.AddConsularAppt(consularApptVM, EmWebAppConfig.QueueNumberInitial);
            Email email = this.GetConfirmationRequestEmail(consularApptVM);
            using (var smtp = new SmtpClient())
            {
                smtp.Prep();
                await smtp.SendMailAsync(email.Message);
            }

            @ViewBag.Title = "Consular Appointment Request - Received";
            AppointmentType appointmentType = ConsularAppointmentTypes.GetAppointmentType(consularApptVM.AppointmentType);
            ViewBag.AppointmentType = appointmentType.Description;

            return View(consularApptVM);
        }

        // GET: ConsularAppt
        [Authorize]  /* For Testing Prefilled Forms */
        public ActionResult Index()
        {

            ViewBag.Title = "Consular Appointment Request - New";
            ConsularApptVM ca = CATestData.GetNewAppt();
            return View(ca);
        }

        [Authorize]  /* For Testing Prefilled Forms */
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Index([Bind(Include = "AppointmentDate,AppointmentType,Name,Gender,DateOfBirth,PlaceOfBirth,Nationality,NRIC_No,PassportNumber,PassportIssuedDate,ConsulateLocation,StayType,StayPermitNumber,EmployerName,Occupation,ContactAddr1,ContactAddr2,ContactPhone,ContactEmail,HomeAddr1,HomeAddr2,HomePhone,Note")] ConsularApptVM consularApptVM)
        {
            if (!ModelState.IsValid)
                this.LogModelStateError();

            EmbassyAppDb.AddConsularAppt(consularApptVM, EmWebAppConfig.QueueNumberInitial);
            Email email = this.GetConfirmationRequestEmail(consularApptVM);
            using (var smtp = new SmtpClient())
            {
                smtp.Prep();
                await smtp.SendMailAsync(email.Message);
            }

            @ViewBag.Title = "Consular Appointment Request - Received";
            AppointmentType appointmentType = ConsularAppointmentTypes.GetAppointmentType(consularApptVM.AppointmentType);
            ViewBag.AppointmentType = appointmentType.Description;

            return View(consularApptVM);
        }

        // GET: ConsularAppt/Confirmed/5
        public async Task<ActionResult> Confirmed(int id, string code)
        {
            DateTime? confirmedApptDate = null;
            int? confirmedQueNumber = 0;
            ConsularApptVM consularApptVM = EmbassyAppDb.ConfirmConsularAppt(id, code, ref confirmedApptDate, ref confirmedQueNumber);
            if (consularApptVM == null)
            {
                return RedirectToAction("Error");
            }

            string confirmedEmailBody = this.GetConfirmedEmailBody(consularApptVM);
            string confirmationLetter = this.GetConfirmationLetter(consularApptVM);
            // To do: Maybe convert the confirmationLetter to PDF, or get fillable PDF letter 

            // Get Required Form Attachment List
            AppointmentType appointmentType = ConsularAppointmentTypes.GetAppointmentType(consularApptVM.AppointmentType);
            List<BLL.Attachment> formAttachments = appointmentType.Attachments?.List;

            // Start preparing for Email
            Email embassyMail = new Email
            {
                From = EmWebAppConfig.EmailAddress,
                DisplayName = EmWebAppConfig.EmailUser,
                To = consularApptVM.ContactEmail,
                Subject = EmWebAppConfig.EmailSubj_Confirmed,
                Body = confirmedEmailBody,
                IsHtml = true
            };

            // using (MemoryStream ms = new MemoryStream())
            string pdfTemplateFile = EmWebAppConfig.CnslrLtrPdfTmplPath + appointmentType.ConsularLtrPdfTmplFilename;

            using (MemoryStream ms = ConfirmationLetterPdf.GetAppointmentLetterStream(pdfTemplateFile, consularApptVM))
            using (MailMessage mailMsg = embassyMail.Message)
            using (var smtp = new SmtpClient())
            {

                System.Net.Mail.Attachment coverLetterPdf = Email.GetPdfAttachmentFromPdfStream(ms, "AppointmentLetter.pdf");
                // System.Net.Mail.Attachment coverLetterPdf = Email.GetPdfAttachmentFromHtmlString(confirmationLetter, ms, "AppointmentLetter.pdf");
                mailMsg.Attachments.Add(coverLetterPdf);

                if (formAttachments != null)
                {
                    foreach (var attachment in formAttachments)
                    {
                        if (System.IO.File.Exists(attachment.FullPath))
                        {
                            var emailAttachment = new System.Net.Mail.Attachment(attachment.FullPath, new ContentType(MediaTypeNames.Application.Pdf));
                            mailMsg.Attachments.Add(emailAttachment);
                        }
                    }
                }

                smtp.Prep();
                await smtp.SendMailAsync(mailMsg);
                return View(consularApptVM);
            }
        }
    }

}