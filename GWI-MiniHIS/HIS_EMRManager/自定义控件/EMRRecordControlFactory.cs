﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.Windows.Forms;

namespace HIS_EMRManager
{
    /// <summary>
    /// 病历控件创建工厂
    /// </summary>
    internal class EMRRecordControlFactory
    {
        /// <summary>
        /// 创建病历控件实体
        /// </summary>
        /// <param name="typeCode">病历类型编码</param>
        /// <returns></returns>
        public static EMRControl CreateEMRRecordControl(string typeCode)
        {
            Control control = new Control();
            switch (typeCode.Trim())
            {
                case "01":
                    control = new 门急诊病历();
                    break;
                case "02":
                    control = new 入院记录();
                    break;
                case "03":
                    control = new 病程记录();
                    break;
                case "04":
                    control = new 出院记录();
                    break;
                case "05":
                    control = new 死亡记录();
                    break;
                case "06":
                    control = new 手术记录();
                    break;
            }
            control.Dock = DockStyle.Fill;
            return (EMRControl)control;
        }

        /// <summary>
        /// 创建病历控件实体
        /// </summary>
        /// <param name="type">病历类型</param>
        /// <returns></returns>
        public static EMRControl CreateEMRRecordControl(Public.EMRType type)
        {
            Control control = new Control();
            switch (type)
            {
                case Public.EMRType.门诊病历:
                    control = new 门急诊病历();
                    break;
                case Public.EMRType.入院记录:
                    control = new 入院记录();
                    break;
                case Public.EMRType.病程记录:
                    control = new 病程记录();
                    break;
                case Public.EMRType.出院记录:
                    control = new 出院记录();
                    break;
                case Public.EMRType.死亡记录:
                    control = new 死亡记录();
                    break;
                case Public.EMRType.手术记录:
                    control = new 手术记录();
                    break;
            }
            control.Dock = DockStyle.Fill;
            return (EMRControl)control;
        }

        /// <summary>
        /// 创建病历控件实体
        /// </summary>
        /// <param name="typeCode">控件编码</param>
        /// <param name="xmlDoc">病历内容数据</param>
        /// <returns></returns>
        public static EMRControl CreateEMRRecordControl(string typeCode,XmlDocument xmlDoc)
        {
            Control control = new Control();
            switch (typeCode.Trim())
            {
                case "01":
                    control = new 门急诊病历(xmlDoc);
                    break;
                case "02":
                    control = new 入院记录(xmlDoc);
                    break;
                case "03":
                    control = new 病程记录(xmlDoc);
                    break;
                case "04":
                    control = new 出院记录(xmlDoc);
                    break;
                case "05":
                    control = new 死亡记录(xmlDoc);
                    break;
                case "06":
                    control = new 手术记录(xmlDoc);
                    break;
            }
            control.Dock = DockStyle.Fill;
            return (EMRControl)control;
        }
        /// <summary>
        /// 创建病历控件实体
        /// </summary>
        /// <param name="type">控件编码</param>
        /// <param name="xmlDoc">病历内容数据</param>
        /// <returns></returns>
        public static EMRControl CreateEMRRecordControl(Public.EMRType type, XmlDocument xmlDoc)
        {
            Control control = new Control();
            switch (type)
            {
                case Public.EMRType.门诊病历:
                    control = new 门急诊病历(xmlDoc);
                    break;
                case Public.EMRType.入院记录:
                    control = new 入院记录(xmlDoc);
                    break;
                case Public.EMRType.病程记录:
                    control = new 病程记录(xmlDoc);
                    break;
                case Public.EMRType.出院记录:
                    control = new 出院记录(xmlDoc);
                    break;
                case Public.EMRType.死亡记录:
                    control = new 死亡记录(xmlDoc);
                    break;
                case Public.EMRType.手术记录:
                    control = new 手术记录(xmlDoc);
                    break;
            }
            control.Dock = DockStyle.Fill;
            return (EMRControl)control;
        }
    }
}
