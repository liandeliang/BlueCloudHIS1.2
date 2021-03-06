﻿using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Text;
using System.Data;
using HIS.SYSTEM.DatabaseAccessLayer;
using HIS.SYSTEM.PubicBaseClasses;
using HIS.SYSTEM.Core;
using HIS.BLL;
using HIS.Model;
using HIS.DAL;

namespace HIS.YP_BLL
{
    /// <summary>
    /// 药库系统月结处理器
    /// </summary>
    public class YK_MonthBalance : MonthBalanceProcessor
    {

        /// <summary>
        /// 定义月结处理事件
        /// </summary>
        public event MonthAccountHandler AccountHandler;
        /// <summary>
        /// 月结对账算法
        /// </summary>
        /// <param name="accountList">月结全部台帐</param>
        /// <returns></returns>
        protected DataTable CheckAccount(List<YP_Account> accountList)
        {
            Hashtable makerdicDt = new Hashtable();
            DataTable wrongDt = base.BuildWrongDataTable();
            foreach (YP_Account act in accountList)
            {
                if (!makerdicDt.ContainsKey(act.MakerDicID))
                {
                    makerdicDt.Add(act.MakerDicID, act);
                }
                else if (act.MAccountID > ((YP_Account)(makerdicDt[act.MakerDicID])).MAccountID)
                {
                    makerdicDt[act.MakerDicID] = act;
                }
            }
            foreach (object makerDicId in makerdicDt)
            {
                YP_Dal dal = new YP_Dal();
                dal._oleDb = oleDb;
                //获取单个药品最后一笔未结台帐信息
                YP_Account hashValue = (YP_Account)((DictionaryEntry)makerDicId).Value;
                //获取单个药品当前库存数量和金额
                decimal storeFee = dal.YK_GetDrugFee(hashValue.DeptID, hashValue.MakerDicID);
                decimal storeNum = StoreFactory.GetQuery(ConfigManager.YK_SYSTEM).QueryNum(hashValue.MakerDicID,
                    hashValue.DeptID);
                _makerDicId = hashValue.MakerDicID;
                //获取当前药品的所有未结台帐
                List<YP_Account> actList = accountList.FindAll(match);
                decimal balanceFee = 0;
                int deptId = 0;
                //获取药品期初金额
                foreach (YP_Account act in actList)
                {
                    if (act.AccountType == 0)
                    {
                        balanceFee = act.BalanceFee;
                    }
                }
                //获取药品按借贷金额累计后的余额
                foreach (YP_Account act in actList)
                {
                    balanceFee += act.LenderFee;
                    balanceFee -= act.DebitFee;
                    deptId = act.DeptID;
                }
                if (Math.Abs(hashValue.BalanceFee - balanceFee) > 2
                   || Math.Abs(hashValue.BalanceFee - storeFee) > 2
                    || Math.Abs(hashValue.OverNum - storeNum) > 0)
                {
                    string drugName = DrugBaseDataBll.GetDurgName(hashValue.MakerDicID);
                    DataRow errorRow = wrongDt.NewRow();
                    errorRow["CHEMNAME"] = drugName;
                    errorRow["MAKERDICID"] = hashValue.MakerDicID;
                    errorRow["WRONGFEE"] = hashValue.BalanceFee - balanceFee;
                    errorRow["STOREWRONGFEE"] = storeFee - hashValue.BalanceFee;
                    errorRow["WRONGNUM"] = storeNum - hashValue.OverNum;
                    errorRow["BALANCEFEE"] = hashValue.BalanceFee;
                    errorRow["BALANCENUM"] = hashValue.OverNum;
                    errorRow["UNIT"] = hashValue.LeastUnit;
                    errorRow["UNITNUM"] = hashValue.UnitNum;
                    wrongDt.Rows.Add(errorRow);
                }
            }
            return wrongDt;
        }

       　/// <summary>
       　/// 药库系统月结对账
       　/// </summary>
       　/// <param name="deptId">药剂科室ID</param>
       　/// <returns>错误账目信息表</returns>
        public override DataTable SystemCheckAccount(int deptId)
        {
            try
            {
                //触发事件
                _monthEvent.CurrentState = MonthAccountState.SystemChecking;
                AccountHandler(_monthEvent);
                IBaseDAL<YP_AccountHis> accountHisDao = BindEntity<YP_AccountHis>.CreateInstanceDAL(oleDb,
                    HIS.BLL.Tables.YK_ACCOUNTHIS);
                int maxId = accountHisDao.GetMaxId(BLL.Tables.yf_accounthis.ACCOUNTHISTORYID,
                    BLL.Tables.yf_accounthis.DEPTID + oleDb.EuqalTo() + deptId.ToString());
                YP_AccountHis prevHis = accountHisDao.GetModel(maxId);
                int checkYear, checkMonth;
                if (prevHis.AccountMonth != 12)
                {
                    checkYear = prevHis.AccountYear;
                    checkMonth = prevHis.AccountMonth + 1;
                }
                else
                {
                    checkYear = prevHis.AccountYear + 1;
                    checkMonth = 1;
                }
                IBaseDAL<YP_Account> accountDao = BindEntity<YP_Account>.CreateInstanceDAL(oleDb, HIS.BLL.Tables.YK_ACCOUNT);
                List<YP_Account> listAccount =
                       accountDao.GetListArray("AccountYear=" + checkYear.ToString() +
                       " AND AccountMonth=" + checkMonth.ToString() + " AND BALANCE_FLAG=0"
                       + " AND DeptId=" + deptId);
                DateTime currentTime = XcDate.ServerDateTime;
                DataTable rtnDt = CheckAccount(listAccount);
                //触发事件
                _monthEvent.CurrentState = MonthAccountState.Over;
                AccountHandler(_monthEvent);
                return rtnDt;
            }
            catch (Exception error)
            {
                throw error;
            }
        }

        /// <summary>
        /// 药品平账
        /// </summary>
        /// <param name="deptId">药剂科室ID</param>
        public override void BalanceAccount(int deptId)
        {
            try
            {
                //获取对账错误的药品信息
                //触发事件
                _monthEvent.CurrentState = MonthAccountState.SystemChecking;
                AccountHandler(_monthEvent);
                DataTable wrongAccountDt = SystemCheckAccount(deptId);
                AccountQuery query = AccountFactory.GetQuery(ConfigManager.YK_SYSTEM);
                IBaseDAL<YP_Account> accountDao = BindEntity<YP_Account>.CreateInstanceDAL(oleDb, BLL.Tables.YK_ACCOUNT);
                if (wrongAccountDt.Rows.Count > 0)
                {
                    //触发事件
                    _monthEvent.CurrentState = MonthAccountState.WriteAdjAccount;
                    AccountHandler(_monthEvent);
                    AdjAccount(deptId, wrongAccountDt, query, accountDao);
                }
                //触发事件
                _monthEvent.CurrentState = MonthAccountState.Over;
                AccountHandler(_monthEvent);
            }
            catch (Exception error)
            {
                if (oleDb.IsInTransaction)
                {
                    oleDb.RollbackTransaction();
                }
                throw error;
            }
        }


        /// <summary>
        /// 药库月结
        /// </summary>
        /// <param name="regPeople">月结操作人员</param>
        /// <param name="regDept">月结药剂科室</param>
        public override void MonthAccount(int regPeople, int regDept)
        {
            try
            {
                //锁住当前库房，确保月结时不会进行其他操作
                ConfigManager.BeginCheck(regDept);
                oleDb.BeginTransaction();
                DateTime currentTime = XcDate.ServerDateTime;
                int accountDay = ConfigManager.GetAccountDay(regDept);
                YP_AccountHis lastAccount = new YK_AccountQuery().GetLastAccountHis(regDept);
                if (lastAccount != null)
                {
                    if (currentTime.Day == accountDay && currentTime.Month != lastAccount.AccountMonth)
                    {
                        currentTime = MonthAccountAction(regPeople, regDept, currentTime, false);
                        oleDb.CommitTransaction();
                    }
                    else
                    {
                        throw new Exception("当前日期不是月结日期或本月已做过月结，无法进行月结");
                    }
                }
                else
                {
                    if (currentTime.Day == accountDay)
                    {
                        MonthAccountAction(regPeople, regDept, currentTime, true);
                        oleDb.CommitTransaction();
                    }
                    else
                    {
                        throw new Exception("当前日期不是月结日期，无法进行月结");
                    }
                }
                //解锁库房
                ConfigManager.EndCheck(regDept);
            }
            catch (Exception error)
            {
                if (oleDb.IsInTransaction)
                {
                    oleDb.RollbackTransaction();
                }
                //解锁库房
                ConfigManager.EndCheck(regDept);
                throw error;
            }
        }
        /// <summary>
        /// 药库月结操作
        /// </summary>
        /// <param name="regPeople">月结人员</param>
        /// <param name="regDept">月结科室</param>
        /// <param name="currentTime">当前时间</param>
        /// <param name="isInit">是否初始化月结</param>
        /// <returns>月结时间</returns>
        protected override DateTime MonthAccountAction(int regPeople, int regDept, DateTime currentTime, bool isInit)
        {
            YP_Dal ypDal = new YP_Dal();
            ypDal._oleDb = oleDb;
            int billNum = 0;
            IBaseDAL<YP_AccountHis> accountHisDao = BindEntity<YP_AccountHis>.CreateInstanceDAL(oleDb, HIS.BLL.Tables.YK_ACCOUNTHIS);
            IBaseDAL<YP_BillNumDic> billDao = BindEntity<YP_BillNumDic>.CreateInstanceDAL(oleDb, HIS.BLL.Tables.YP_BILLNUMDIC);
            IBaseDAL<YP_Account> accountDao = BindEntity<YP_Account>.CreateInstanceDAL(oleDb, HIS.BLL.Tables.YK_ACCOUNT);
            IBaseDAL<YP_Storage> ykStore = BindEntity<YP_Storage>.CreateInstanceDAL(oleDb, HIS.BLL.Tables.YK_STORAGE);
            //初始化月结
            if (isInit)
            {
                YP_AccountHis accoutHis = new YP_AccountHis();
                accoutHis.AccountMonth = currentTime.Month;
                accoutHis.AccountYear = currentTime.Year;
                accoutHis.BeginTime = accoutHis.EndTime = currentTime;
                accoutHis.DeptID = regDept;
                accoutHis.RegMan = regPeople;
                accoutHis.RegTime = currentTime;
                accountHisDao.Add(accoutHis);
                //写月结期初台帐
                //触发事件
                _monthEvent.CurrentState = MonthAccountState.WriteBeginAccount;
                AccountHandler(_monthEvent);
                billNum = ypDal.YP_Bill_GetBillNum(ConfigManager.OP_YK_MONTHACCOUNT, regDept).BillNum;
                WriteBeginDateAccount(accoutHis, billNum, accountDao, regDept);
            }
            else
            {
                int maxId = accountHisDao.GetMaxId(Tables.yk_accounthis.ACCOUNTHISTORYID, "DEPTID=" + regDept.ToString());
                YP_AccountHis prevHis = accountHisDao.GetModel(maxId);
                YP_AccountHis currentHis = new YP_AccountHis();
                if (prevHis.AccountMonth != 12)
                {
                    currentHis.AccountMonth = prevHis.AccountMonth + 1;
                    currentHis.AccountYear = prevHis.AccountYear;
                }
                else
                {
                    currentHis.AccountMonth = 1;
                    currentHis.AccountYear = prevHis.AccountYear + 1;
                }
                currentHis.BeginTime = prevHis.EndTime;
                currentHis.DeptID = regDept;
                currentHis.EndTime = currentTime;
                currentHis.RegMan = regPeople;
                currentHis.RegTime = currentTime;
                accountHisDao.Add(currentHis);
                billNum = ypDal.YP_Bill_GetBillNum(ConfigManager.OP_YK_MONTHACCOUNT, regDept).BillNum;
                //触发事件
                _monthEvent.CurrentState = MonthAccountState.SystemChecking;
                AccountHandler(_monthEvent);
                string strWhere = "AccountYear=" + currentHis.AccountYear +
                    " AND AccountMonth=" + currentHis.AccountMonth +
                    " AND BALANCE_FLAG=0" +
                    " AND DeptId=" + regDept;
                List<YP_Account> listAccount = accountDao.GetListArray(strWhere);
                if (CheckAccount(listAccount).Rows.Count > 0)
                {
                    throw new Exception("药品账目错误，无法月结，请进行系统对账，查看明细");
                }
                accountDao.Update(strWhere, BLL.Tables.yk_account.BALANCE_FLAG + oleDb.EuqalTo() + "1",
                    BLL.Tables.yk_account.ACCOUNTHISTORYID + oleDb.EuqalTo() + currentHis.AccountHistoryID.ToString());
                List<YP_Account> endActList = base.GetMonthEndData(listAccount, billNum, currentTime, currentHis.AccountHistoryID);

                //写期末记录
                //触发事件
                _monthEvent.CurrentState = MonthAccountState.WriteEndAccount;
                AccountHandler(_monthEvent);
                WriteEndDateAccount(endActList, accountDao, ykStore);
                //写期初记录
                //触发事件
                _monthEvent.CurrentState = MonthAccountState.WriteBeginAccount;
                AccountHandler(_monthEvent);
                WriteBeginDateAccount(endActList, accountDao);
            }
            //触发事件
            _monthEvent.CurrentState = MonthAccountState.Over;
            AccountHandler(_monthEvent);
            return currentTime;
        }

        /// <summary>
        /// 取消药库月结
        /// </summary>
        /// <param name="regPeople">月结人员</param>
        /// <param name="regDept">月结科室</param>
        public override void CancelMonthAccount(int regPeople, int regDept)
        {
            try
            {
                oleDb.BeginTransaction();
                string strWhere = "";
                //获取当前时间
                DateTime currentTime = XcDate.ServerDateTime;
                //获取月结日
                int accountDay = ConfigManager.GetAccountDay(regDept);
                //获取上次月结记录
                YP_AccountHis lastAccount = new YK_AccountQuery().GetLastAccountHis(regDept);
                if (lastAccount == null)
                {
                    throw new Exception("没有月结历史记录，请先进行初始化月结");
                }
                if (BindEntity<YP_AccountHis>.CreateInstanceDAL(oleDb, BLL.Tables.YK_ACCOUNTHIS).GetListArray(BLL.Tables.yk_accounthis.DEPTID
                    + oleDb.EuqalTo() + lastAccount.DeptID).Count <= 1)
                {
                    throw new Exception("初始化月结不允许取消");
                }
                //判断是否可以取消月结
                if (currentTime.Month == lastAccount.AccountMonth && currentTime > lastAccount.RegTime)
                {
                    IBaseDAL<YP_AccountHis> accountHisDao = BindEntity<YP_AccountHis>.CreateInstanceDAL(oleDb,
                        HIS.BLL.Tables.YK_ACCOUNTHIS);
                    IBaseDAL<YP_Account> accountDao = BindEntity<YP_Account>.CreateInstanceDAL(oleDb, HIS.BLL.Tables.YK_ACCOUNT);
                    //删除前次月结历史记录
                    accountHisDao.Delete(lastAccount.AccountHistoryID);
                    //删除本月期末台帐
                    strWhere = "AccountMonth=" + currentTime.Month.ToString() + " AND " + "AccountType=1" +
                        " AND DeptID=" + regDept.ToString();
                    accountDao.Delete(strWhere);
                    int nextMonth;
                    if (currentTime.Month == 12)
                    {
                        nextMonth = 1;
                    }
                    else
                    {
                        nextMonth = currentTime.Month + 1;
                    }
                    //删除下月月期初台帐
                    strWhere = "AccountMonth=" + nextMonth.ToString() + " AND " + "AccountType=0" +
                        " AND DeptID=" + regDept.ToString();
                    accountDao.Delete(strWhere);
                    //将已经月结过的台帐记录设置成未月结
                    strWhere = "AccountMonth=" + currentTime.Month.ToString() +
                        " AND DeptID=" + regDept.ToString();
                    accountDao.Update(strWhere, "Balance_Flag=0");
                    //将下月的台帐记录中的会计月记录设置成当前月
                    strWhere = "AccountMonth=" + nextMonth.ToString() +
                        " AND DeptID=" + regDept.ToString();
                    accountDao.Update(strWhere, "AccountMonth=" + currentTime.Month.ToString());
                    oleDb.CommitTransaction();
                }
                else
                {
                    throw new Exception("本月还没有进行月结，因此无法取消月结");
                }
            }
            catch (Exception error)
            {
                oleDb.RollbackTransaction();
                throw error;
            }
        }
        /// <summary>
        /// 写药库期初台帐
        /// </summary>
        /// <param name="accountList">所有药品月结前最后一笔台帐记录</param>
        /// <param name="accountDao">台帐表操作对象</param>
        protected override void WriteBeginDateAccount(List<YP_Account> accountList, IBaseDAL<YP_Account> accountDao)
        {
            try
            {
                foreach (YP_Account account in accountList)
                {
                    if (account.AccountMonth == 12)
                    {
                        account.AccountYear++;
                        account.AccountMonth = 1;
                    }
                    else
                    {
                        account.AccountMonth++;
                    }
                    account.Balance_Flag = 0;
                    account.AccountType = 0;
                    account.OpType = ConfigManager.OP_YK_MONTHACCOUNT;
                    accountDao.Add(account);
                }
            }
            catch (Exception error)
            {
                throw error;
            }
        }

        /// <summary>
        /// 写药库期初台帐
        /// </summary>
        /// <param name="accountHis">最后一次月结历史信息</param>
        /// <param name="billNum">单据号</param>
        /// <param name="accountDao">台帐表操作对象</param>
        /// <param name="deptId">月结科室ＩＤ</param>
        protected override void WriteBeginDateAccount(YP_AccountHis accountHis, int billNum, IBaseDAL<YP_Account> accountDao,
            int deptId)
        {
            YP_Dal ypDal = new YP_Dal();
            ypDal._oleDb = oleDb;
            YP_Account account = new YP_Account();
            DataTable beginDataDt = ypDal.YK_Store_GetListForAccount(deptId);
            for (int index = 0; index < beginDataDt.Rows.Count; index++)
            {
                DataRow dRow = beginDataDt.Rows[index];
                if (accountHis.RegTime.Month != 12)
                {
                    account.AccountMonth = accountHis.RegTime.Month + 1;
                    account.AccountYear = accountHis.RegTime.Year;
                }
                else
                {
                    account.AccountMonth = 1;
                    account.AccountYear++;
                }
                account.Balance_Flag = 0;
                account.BillNum = billNum;
                account.DeptID = deptId;
                account.AccountHistoryID = accountHis.AccountHistoryID;
                account.LeastUnit = Convert.ToInt32(dRow["PACKUNIT"]);
                account.MakerDicID = Convert.ToInt32(dRow["MAKERDICID"]);
                account.OpType = ConfigManager.OP_YK_MONTHACCOUNT;
                account.OrderID = 0;
                account.RegTime = accountHis.RegTime;
                account.RetailPrice = Convert.ToDecimal(dRow["RETAILPRICE"]);
                account.StockPrice = Convert.ToDecimal(dRow["TRADEPRICE"]);
                account.UnitNum = Convert.ToInt32(dRow["PUNITNUM"]);
                account.OverNum = Convert.ToInt32(dRow["CURRENTNUM"]);
                account.BalanceFee = account.OverNum * account.RetailPrice;
                accountDao.Add(account);
            }
        }
        /// <summary>
        /// 写药库期末台帐
        /// </summary>
        /// <param name="accountList">台帐信息明细</param>
        /// <param name="accountDao">台帐表操作对象</param>
        /// <param name="ypStore">药品库存表操作对象</param>
        protected override void WriteEndDateAccount(List<YP_Account> accountList, IBaseDAL<YP_Account> accountDao,
            IBaseDAL<YP_Storage> ypStore)
        {
            try
            {
                foreach (YP_Account account in accountList)
                {
                    account.Balance_Flag = 1;
                    account.AccountType = 1;
                    account.OpType = ConfigManager.OP_YK_MONTHACCOUNT;
                    accountDao.Add(account);
                }
            }
            catch (Exception error)
            {
                throw error;
            }
        }
    }
}
