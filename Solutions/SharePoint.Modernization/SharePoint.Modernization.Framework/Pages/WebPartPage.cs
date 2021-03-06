﻿using Microsoft.SharePoint.Client;
using Microsoft.SharePoint.Client.WebParts;
using SharePoint.Modernization.Framework.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharePoint.Modernization.Framework.Pages
{
    /// <summary>
    /// Analyzes a web part page
    /// </summary>
    public class WebPartPage: BasePage
    {

        private enum WebPartPageLayout
        {
            HeaderFooterThreeColumns,
            FullPageVertical,
            HeaderLeftColumnBody,
            HeaderRightColumnBody,
            HeaderFooter2Columns4Rows,
            HeaderFooter4ColumnsTopRow,
            LeftColumnHeaderFooterTopRow3Columns,
            RightColumnHeaderFooterTopRow3Columns,
            Custom
        }

        private const string FileRefField = "FileRef";

        #region construction
        /// <summary>
        /// 
        /// </summary>
        /// <param name="page"></param>
        /// <param name="pageTransformation"></param>
        /// <param name="forceCheckout"></param>
        public WebPartPage(ListItem page, PageTransformation pageTransformation) : base(page, pageTransformation)
        {
        }
        #endregion

        /// <summary>
        /// Analyses a webpart page
        /// </summary>
        /// <returns>Information about the analyzed webpart page</returns>
        public Tuple<string, List<WebPartEntity>> Analyze()
        {
            List<WebPartEntity> webparts = new List<WebPartEntity>();

            //Load the page
            var wikiPageUrl = page[FileRefField].ToString();
            var wikiPage = cc.Web.GetFileByServerRelativeUrl(wikiPageUrl);

            // Load page properties
            var pageProperties = wikiPage.Properties;
            cc.Load(pageProperties);

            // Load web parts on wiki page
            var limitedWPManager = wikiPage.GetLimitedWebPartManager(PersonalizationScope.Shared);
            cc.Load(limitedWPManager);

            IEnumerable<WebPartDefinition> webParts = cc.LoadQuery(limitedWPManager.WebParts.IncludeWithDefaultProperties(wp => wp.Id, wp => wp.ZoneId, wp => wp.WebPart.ExportMode, wp => wp.WebPart.Title, wp => wp.WebPart.ZoneIndex, wp => wp.WebPart.IsClosed, wp => wp.WebPart.Hidden, wp => wp.WebPart.Properties));
            cc.ExecuteQueryRetry();

            // Check page type
            var layout = GetLayout(pageProperties);
            
            if (webParts.Count() > 0)
            {
                if (forceCheckout)
                {
                    wikiPage.CheckOut();
                    cc.ExecuteQueryRetry();
                }

                foreach (var foundWebPart in webParts)
                {
                    // Skip Microsoft.SharePoint.WebPartPages.TitleBarWebPart webpart in TitleBar zone
                    if (foundWebPart.ZoneId.Equals("TitleBar", StringComparison.InvariantCultureIgnoreCase))
                    {
                        continue;
                    }

                    var changed = false;
                    var exportMode = foundWebPart.WebPart.ExportMode;
                    if (foundWebPart.WebPart.ExportMode != WebPartExportMode.All)
                    {
                        foundWebPart.WebPart.ExportMode = WebPartExportMode.All;
                        foundWebPart.SaveWebPartChanges();
                        cc.ExecuteQueryRetry();
                        changed = true;
                    }

                    var result = limitedWPManager.ExportWebPart(foundWebPart.Id);
                    cc.ExecuteQueryRetry();
                    string webPartXml = result.Value;

                    if (changed)
                    {
                        foundWebPart.WebPart.ExportMode = exportMode;
                        foundWebPart.SaveWebPartChanges();
                        cc.ExecuteQueryRetry();
                    }

                    string webPartType = GetType(webPartXml);

                    webparts.Add(new WebPartEntity()
                    {
                        Title = foundWebPart.WebPart.Title,
                        Type = webPartType,
                        Id = foundWebPart.Id,
                        ServerControlId = foundWebPart.Id.ToString(),
                        Row = GetRow(foundWebPart.ZoneId, layout),
                        Column = GetColumn(foundWebPart.ZoneId, layout),
                        Order = foundWebPart.WebPart.ZoneIndex,
                        ZoneId = foundWebPart.ZoneId,
                        ZoneIndex = (uint)foundWebPart.WebPart.ZoneIndex,
                        IsClosed = foundWebPart.WebPart.IsClosed,
                        Hidden = foundWebPart.WebPart.Hidden,
                        Properties = Properties(foundWebPart.WebPart.Properties, webPartType, webPartXml),
                    });
                }

                if (forceCheckout)
                {
                    wikiPage.UndoCheckOut();
                    cc.ExecuteQueryRetry();
                }
            }

            return new Tuple<string, List<WebPartEntity>>(layout.ToString(), webparts);
        }

        /// <summary>
        /// Translates the given zone value and page layout to a column number
        /// </summary>
        /// <param name="zoneId">Web part zone id</param>
        /// <param name="layout">Layout of the web part page</param>
        /// <returns>Column value</returns>
        private int GetColumn(string zoneId, WebPartPageLayout layout)
        {
            switch (layout)
            {
                case WebPartPageLayout.HeaderFooterThreeColumns:
                    {
                        if (zoneId.Equals("Header", StringComparison.InvariantCultureIgnoreCase) ||
                            zoneId.Equals("LeftColumn", StringComparison.InvariantCultureIgnoreCase) ||
                            zoneId.Equals("Footer", StringComparison.InvariantCultureIgnoreCase))
                        {
                            return 1;
                        }
                        else if (zoneId.Equals("MiddleColumn", StringComparison.InvariantCultureIgnoreCase))
                        {
                            return 2;
                        }
                        else if (zoneId.Equals("RightColumn", StringComparison.InvariantCultureIgnoreCase))
                        {
                            return 3;
                        }
                        break;
                    }
                case WebPartPageLayout.FullPageVertical:
                    {
                        return 1;
                    }
                case WebPartPageLayout.HeaderLeftColumnBody:
                    {
                        if (zoneId.Equals("Header", StringComparison.InvariantCultureIgnoreCase) ||
                            zoneId.Equals("LeftColumn", StringComparison.InvariantCultureIgnoreCase))
                        {
                            return 1;
                        }
                        else if (zoneId.Equals("Body", StringComparison.InvariantCultureIgnoreCase))
                        {
                            return 2;
                        }
                        break;
                    }
                case WebPartPageLayout.HeaderRightColumnBody:
                    {
                        if (zoneId.Equals("Header", StringComparison.InvariantCultureIgnoreCase) ||
                            zoneId.Equals("Body", StringComparison.InvariantCultureIgnoreCase))
                        {
                            return 1;
                        }
                        else if (zoneId.Equals("RightColumn", StringComparison.InvariantCultureIgnoreCase))
                        {
                            return 2;
                        }
                        break;
                    }
                case WebPartPageLayout.HeaderFooter2Columns4Rows:
                    {
                        if (zoneId.Equals("Header", StringComparison.InvariantCultureIgnoreCase) ||
                            zoneId.Equals("Footer", StringComparison.InvariantCultureIgnoreCase) ||
                            zoneId.Equals("LeftColumn", StringComparison.InvariantCultureIgnoreCase))
                        {
                            return 1;
                        }
                        else if (zoneId.Equals("Row1", StringComparison.InvariantCultureIgnoreCase) ||
                                 zoneId.Equals("Row2", StringComparison.InvariantCultureIgnoreCase) ||
                                 zoneId.Equals("Row3", StringComparison.InvariantCultureIgnoreCase) ||
                                 zoneId.Equals("Row4", StringComparison.InvariantCultureIgnoreCase))
                        {
                            return 2;
                        }
                        else if (zoneId.Equals("RightColumn", StringComparison.InvariantCultureIgnoreCase))
                        {
                            return 3;
                        }
                        break;
                    }
                case WebPartPageLayout.HeaderFooter4ColumnsTopRow:
                    {
                        if (zoneId.Equals("Header", StringComparison.InvariantCultureIgnoreCase) ||
                            zoneId.Equals("Footer", StringComparison.InvariantCultureIgnoreCase) ||
                            zoneId.Equals("LeftColumn", StringComparison.InvariantCultureIgnoreCase) ||
                            zoneId.Equals("CenterLeftColumn", StringComparison.InvariantCultureIgnoreCase))
                        {
                            return 1;
                        }
                        else if (zoneId.Equals("TopRow", StringComparison.InvariantCultureIgnoreCase) ||
                                 zoneId.Equals("CenterRightColumn", StringComparison.InvariantCultureIgnoreCase))
                        {
                            return 2;
                        }
                        else if (zoneId.Equals("RightColumn", StringComparison.InvariantCultureIgnoreCase))
                        {
                            return 3;
                        }
                        break;
                    }
                case WebPartPageLayout.LeftColumnHeaderFooterTopRow3Columns:
                    {
                        if (zoneId.Equals("Header", StringComparison.InvariantCultureIgnoreCase) ||
                            zoneId.Equals("LeftColumn", StringComparison.InvariantCultureIgnoreCase) ||
                            zoneId.Equals("CenterLeftColumn", StringComparison.InvariantCultureIgnoreCase) ||
                            zoneId.Equals("Footer", StringComparison.InvariantCultureIgnoreCase) ||
                            zoneId.Equals("TopRow", StringComparison.InvariantCultureIgnoreCase))
                        {
                            return 1;
                        }
                        else if (zoneId.Equals("CenterColumn", StringComparison.InvariantCultureIgnoreCase))
                        {
                            return 2;
                        }
                        else if (zoneId.Equals("CenterRightColumn", StringComparison.InvariantCultureIgnoreCase))
                        {
                            return 3;
                        }                        
                        break;
                    }
                case WebPartPageLayout.RightColumnHeaderFooterTopRow3Columns:
                    {
                        if (zoneId.Equals("Header", StringComparison.InvariantCultureIgnoreCase) ||
                            zoneId.Equals("RightColumn", StringComparison.InvariantCultureIgnoreCase) ||
                            zoneId.Equals("CenterLeftColumn", StringComparison.InvariantCultureIgnoreCase) ||
                            zoneId.Equals("Footer", StringComparison.InvariantCultureIgnoreCase) ||
                            zoneId.Equals("TopRow", StringComparison.InvariantCultureIgnoreCase))
                        {
                            return 1;
                        }
                        else if (zoneId.Equals("CenterColumn", StringComparison.InvariantCultureIgnoreCase))
                        {
                            return 2;
                        }
                        else if (zoneId.Equals("CenterRightColumn", StringComparison.InvariantCultureIgnoreCase))
                        {
                            return 3;
                        }
                        break;
                    }
                case WebPartPageLayout.Custom:
                    {
                        return 1;
                    }
                default:
                    return 1;
            }

            return 1;
        }

        /// <summary>
        /// Translates the given zone value and page layout to a row number
        /// </summary>
        /// <param name="zoneId">Web part zone id</param>
        /// <param name="layout">Layout of the web part page</param>
        /// <returns>Row value</returns>
        private int GetRow(string zoneId, WebPartPageLayout layout)
        {
            switch (layout)
            {
                case WebPartPageLayout.HeaderFooterThreeColumns:
                    {
                        if (zoneId.Equals("Header", StringComparison.InvariantCultureIgnoreCase))
                        {
                            return 1;
                        }
                        else if (zoneId.Equals("LeftColumn", StringComparison.InvariantCultureIgnoreCase) ||
                                 zoneId.Equals("MiddleColumn", StringComparison.InvariantCultureIgnoreCase) ||
                                 zoneId.Equals("RightColumn", StringComparison.InvariantCultureIgnoreCase) )
                        {
                            return 2;
                        }
                        else if (zoneId.Equals("Footer", StringComparison.InvariantCultureIgnoreCase))
                        {
                            return 3;
                        }
                        break;
                    }
                case WebPartPageLayout.FullPageVertical:
                    {
                        return 1;                        
                    }
                case WebPartPageLayout.HeaderLeftColumnBody:
                    {
                        if (zoneId.Equals("Header", StringComparison.InvariantCultureIgnoreCase))
                        {
                            return 1;
                        }
                        else if (zoneId.Equals("LeftColumn", StringComparison.InvariantCultureIgnoreCase) ||
                                 zoneId.Equals("Body", StringComparison.InvariantCultureIgnoreCase))
                        {
                            return 2;
                        }
                        break;
                    }
                case WebPartPageLayout.HeaderRightColumnBody:
                    {
                        if (zoneId.Equals("Header", StringComparison.InvariantCultureIgnoreCase))
                        {
                            return 1;
                        }
                        else if (zoneId.Equals("RightColumn", StringComparison.InvariantCultureIgnoreCase) ||
                                 zoneId.Equals("Body", StringComparison.InvariantCultureIgnoreCase))
                        {
                            return 2;
                        }
                        break;
                    }
                case WebPartPageLayout.HeaderFooter2Columns4Rows:
                    {
                        if (zoneId.Equals("Header", StringComparison.InvariantCultureIgnoreCase))
                        {
                            return 1;
                        }
                        else if (zoneId.Equals("LeftColumn", StringComparison.InvariantCultureIgnoreCase) ||
                                 zoneId.Equals("Row1", StringComparison.InvariantCultureIgnoreCase) ||
                                 zoneId.Equals("RightColumn", StringComparison.InvariantCultureIgnoreCase))
                        {
                            return 2;
                        }
                        else if (zoneId.Equals("Row2", StringComparison.InvariantCultureIgnoreCase))
                        {
                            return 3;
                        }
                        else if (zoneId.Equals("Row3", StringComparison.InvariantCultureIgnoreCase))
                        {
                            return 4;
                        }
                        else if (zoneId.Equals("Row4", StringComparison.InvariantCultureIgnoreCase))
                        {
                            return 5;
                        }
                        else if (zoneId.Equals("Footer", StringComparison.InvariantCultureIgnoreCase))
                        {
                            return 6;
                        }
                        break;
                    }
                case WebPartPageLayout.HeaderFooter4ColumnsTopRow:
                    {
                        if (zoneId.Equals("Header", StringComparison.InvariantCultureIgnoreCase))
                        {
                            return 1;
                        }
                        else if (zoneId.Equals("LeftColumn", StringComparison.InvariantCultureIgnoreCase) ||
                                 zoneId.Equals("TopRow", StringComparison.InvariantCultureIgnoreCase) ||
                                 zoneId.Equals("RightColumn", StringComparison.InvariantCultureIgnoreCase))
                        {
                            return 2;
                        }
                        else if (zoneId.Equals("CenterLeftColumn", StringComparison.InvariantCultureIgnoreCase) ||
                                 zoneId.Equals("CenterRightColumn", StringComparison.InvariantCultureIgnoreCase))
                        {
                            return 3;
                        }
                        else if (zoneId.Equals("Footer", StringComparison.InvariantCultureIgnoreCase))
                        {
                            return 4;
                        }
                        break;
                    }
                case WebPartPageLayout.LeftColumnHeaderFooterTopRow3Columns:
                    {
                        if (zoneId.Equals("Header", StringComparison.InvariantCultureIgnoreCase) ||
                            zoneId.Equals("LeftColumn", StringComparison.InvariantCultureIgnoreCase))
                        {
                            return 1;
                        }
                        else if (zoneId.Equals("TopRow", StringComparison.InvariantCultureIgnoreCase))
                        {
                            return 2;
                        }
                        else if (zoneId.Equals("CenterLeftColumn", StringComparison.InvariantCultureIgnoreCase) ||
                                 zoneId.Equals("CenterColumn", StringComparison.InvariantCultureIgnoreCase) ||
                                 zoneId.Equals("CenterRightColumn", StringComparison.InvariantCultureIgnoreCase))
                        {
                            return 3;
                        }
                        else if (zoneId.Equals("Footer", StringComparison.InvariantCultureIgnoreCase))
                        {
                            return 4;
                        }
                        break;
                    }
                case WebPartPageLayout.RightColumnHeaderFooterTopRow3Columns:
                    {
                        if (zoneId.Equals("Header", StringComparison.InvariantCultureIgnoreCase) ||
                            zoneId.Equals("RightColumn", StringComparison.InvariantCultureIgnoreCase))
                        {
                            return 1;
                        }
                        else if (zoneId.Equals("TopRow", StringComparison.InvariantCultureIgnoreCase))
                        {
                            return 2;
                        }
                        else if (zoneId.Equals("CenterLeftColumn", StringComparison.InvariantCultureIgnoreCase) ||
                                 zoneId.Equals("CenterColumn", StringComparison.InvariantCultureIgnoreCase) ||
                                 zoneId.Equals("CenterRightColumn", StringComparison.InvariantCultureIgnoreCase))
                        {
                            return 3;
                        }
                        else if (zoneId.Equals("Footer", StringComparison.InvariantCultureIgnoreCase))
                        {
                            return 4;
                        }
                        break;
                    }
                case WebPartPageLayout.Custom:
                    {
                        return 1;
                    }
                default:
                    return 1;
            }

            return 1;
        }

        /// <summary>
        /// Determines the used web part page layout
        /// </summary>
        /// <param name="pageProperties">Properties of the web part page file</param>
        /// <returns>Used layout</returns>
        private WebPartPageLayout GetLayout(PropertyValues pageProperties)
        {
            if (pageProperties.FieldValues.ContainsKey("vti_setuppath"))
            {
                var setupPath = pageProperties["vti_setuppath"].ToString();
                if (!string.IsNullOrEmpty(setupPath))
                {
                    if (setupPath.Equals(@"1033\STS\doctemp\smartpgs\spstd1.aspx", StringComparison.InvariantCultureIgnoreCase))
                    {
                        return WebPartPageLayout.FullPageVertical;
                    }
                    else if (setupPath.Equals(@"1033\STS\doctemp\smartpgs\spstd2.aspx", StringComparison.InvariantCultureIgnoreCase))
                    {
                        return WebPartPageLayout.HeaderFooterThreeColumns; 
                    }
                    else if (setupPath.Equals(@"1033\STS\doctemp\smartpgs\spstd3.aspx", StringComparison.InvariantCultureIgnoreCase))
                    {
                        return WebPartPageLayout.HeaderLeftColumnBody;
                    }
                    else if (setupPath.Equals(@"1033\STS\doctemp\smartpgs\spstd4.aspx", StringComparison.InvariantCultureIgnoreCase))
                    {
                        return WebPartPageLayout.HeaderRightColumnBody;
                    }
                    else if (setupPath.Equals(@"1033\STS\doctemp\smartpgs\spstd5.aspx", StringComparison.InvariantCultureIgnoreCase))
                    {
                        return WebPartPageLayout.HeaderFooter2Columns4Rows;
                    }
                    else if (setupPath.Equals(@"1033\STS\doctemp\smartpgs\spstd6.aspx", StringComparison.InvariantCultureIgnoreCase))
                    {
                        return WebPartPageLayout.HeaderFooter4ColumnsTopRow;
                    }
                    else if (setupPath.Equals(@"1033\STS\doctemp\smartpgs\spstd7.aspx", StringComparison.InvariantCultureIgnoreCase))
                    {
                        return WebPartPageLayout.LeftColumnHeaderFooterTopRow3Columns;
                    }
                    else if (setupPath.Equals(@"1033\STS\doctemp\smartpgs\spstd8.aspx", StringComparison.InvariantCultureIgnoreCase))
                    {
                        return WebPartPageLayout.RightColumnHeaderFooterTopRow3Columns;
                    }
                }
            }

            return WebPartPageLayout.Custom;
        }

    }
}
