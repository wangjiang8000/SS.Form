﻿using System;
using System.Collections.Generic;
using System.Web.Http;
using SiteServer.Plugin;
using SS.Form.Core;
using SS.Form.Core.Model;
using SS.Form.Core.Provider;
using SS.Form.Core.Utils;
using SS.SMS;

namespace SS.Form.Controllers
{
    public class FormController : ApiController
    {
        [HttpPost, Route("{siteId:int}/{formId:int}/actions/get")]
        public IHttpActionResult GetForm(int siteId, int formId)
        {
            try
            {
                var formInfo = FormManager.GetFormInfo(siteId, formId);
                if (formInfo == null) return NotFound();
                if (formInfo.Additional.IsClosed)
                {
                    return BadRequest("对不起，表单已被禁用");
                }

                if (formInfo.Additional.IsTimeout && (formInfo.Additional.TimeToStart > DateTime.Now || formInfo.Additional.TimeToEnd < DateTime.Now))
                {
                    return BadRequest("对不起，表单只允许在规定的时间内提交");
                }

                var fieldInfoList = FieldManager.GetFieldInfoList(formInfo.Id);

                return Ok(new
                {
                    Value = fieldInfoList,
                    formInfo.Title,
                    formInfo.Description,
                    formInfo.Additional.IsCaptcha
                });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        [HttpPost, Route("{siteId:int}/{formId:int}")]
        public IHttpActionResult Submit(int siteId, int formId)
        {
            try
            {
                var request = Context.GetCurrentRequest();

                var formInfo = FormManager.GetFormInfo(siteId, formId);
                if (formInfo == null) return NotFound();
                if (formInfo.Additional.IsClosed)
                {
                    return BadRequest("对不起，表单已被禁用");
                }

                if (formInfo.Additional.IsTimeout && (formInfo.Additional.TimeToStart > DateTime.Now || formInfo.Additional.TimeToEnd < DateTime.Now))
                {
                    return BadRequest("对不起，表单只允许在规定的时间内提交");
                }

                var logInfo = new LogInfo
                {
                    FormId = formInfo.Id,
                    AddDate = DateTime.Now
                };

                var fieldInfoList = FieldManager.GetFieldInfoList(formInfo.Id);
                foreach (var fieldInfo in fieldInfoList)
                {
                    var value = request.GetPostString(fieldInfo.Title);
                    logInfo.Set(fieldInfo.Title, value);
                    if (FieldManager.IsExtra(fieldInfo))
                    {
                        foreach (var item in fieldInfo.Items)
                        {
                            var extrasId = FieldManager.GetExtrasId(fieldInfo.Id, item.Id);
                            var extras = request.GetPostString(extrasId);
                            if (!string.IsNullOrEmpty(extras))
                            {
                                logInfo.Set(extrasId, extras);
                            }
                        }
                    }
                }

                logInfo.Id = LogDao.Insert(formInfo, logInfo);

                if (formInfo.Additional.IsAdministratorSmsNotify && !string.IsNullOrEmpty(formInfo.Additional.AdministratorSmsNotifyTplId) && !string.IsNullOrEmpty(formInfo.Additional.AdministratorSmsNotifyKeys) && !string.IsNullOrEmpty(formInfo.Additional.AdministratorSmsNotifyMobile))
                {
                    var smsPlugin = Context.PluginApi.GetPlugin<SmsPlugin>();
                    if (smsPlugin != null && smsPlugin.IsReady)
                    {
                        var parameters = new Dictionary<string, string>();
                        var keys = formInfo.Additional.AdministratorSmsNotifyKeys.Split(',');
                        foreach (var key in keys)
                        {
                            if (key == nameof(LogInfo.Id))
                            {
                                parameters.Add(key, logInfo.Id.ToString());
                            }
                            else if (key == nameof(LogInfo.AddDate))
                            {
                                parameters.Add(key, logInfo.AddDate.ToString("yyyy-MM-dd HH:mm"));
                            }
                            else
                            {
                                parameters.Add(key, logInfo.GetString(key));
                            }
                        }
                        smsPlugin.Send(formInfo.Additional.AdministratorSmsNotifyMobile, formInfo.Additional.AdministratorSmsNotifyTplId, parameters, out _);
                    }
                }

                return Ok(logInfo);
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        [HttpGet, Route("{siteId:int}/{formId:int}")]
        public IHttpActionResult List(int siteId, int formId)
        {
            try
            {
                var request = Context.GetCurrentRequest();

                var formInfo = FormManager.GetFormInfo(siteId, formId);
                if (formInfo == null) return NotFound();

                var fieldInfoList = FieldManager.GetFieldInfoList(formInfo.Id);
                var listAttributeNames = FormUtils.StringCollectionToStringList(formInfo.Additional.ListAttributeNames);
                var allAttributeNames = FormManager.GetAllAttributeNames(formInfo, fieldInfoList);

                var pages = Convert.ToInt32(Math.Ceiling((double)formInfo.TotalCount / FormUtils.PageSize));
                if (pages == 0) pages = 1;
                var page = request.GetQueryInt("page", 1);
                if (page > pages) page = pages;
                var logInfoList = LogDao.GetLogInfoList(formInfo, formInfo.IsReply, page);

                var logs = new List<Dictionary<string, object>>();
                foreach (var logInfo in logInfoList)
                {
                    logs.Add(logInfo.ToDictionary());
                }

                return Ok(new
                {
                    Value = logs,
                    Count = formInfo.TotalCount,
                    Pages = pages,
                    Page = page,
                    FieldInfoList = fieldInfoList,
                    AllAttributeNames = allAttributeNames,
                    ListAttributeNames = listAttributeNames,
                    formInfo.IsReply
                });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }
    }
}
