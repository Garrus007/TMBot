﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using AutoMapper;
using TMBot.API.Exceptions;
using TMBot.API.Factory;
using TMBot.API.SteamAPI;
using TMBot.API.TMAPI;
using TMBot.API.TMAPI.Models;
using TMBot.Data;
using TMBot.Database;
using TMBot.Models;
using TMBot.Settings;
using TMBot.Utilities;
using TMBot.Utilities.MVVM;
using TMBot.ViewModels.ViewModels;

namespace TMBot.Workers
{
	/// <summary>
	/// Выполняет мониторинг и изменнеие цены продажи
	/// предметов в фоне
	/// </summary>
	/// <typeparam name="TTMAPI">Класс АПИ площадки</typeparam>
	public class SellWorker<TTMAPI,TSteamAPI> : BaseItemWorker<TTMAPI, TSteamAPI, Trade> where TTMAPI : ITMAPI where TSteamAPI : ISteamAPI
    {

	    public SellWorker(SynchronizedObservableCollection<TradeItemViewModel> items) : base(items)
        {
            //Загрузка порога цены
	        var settings = SettingsManager.LoadSettings();
	        PriceThreshold = settings.TradeMaxThreshold;
	    } 


        //Показывает сообщение об ошибке
        protected override void ShowErrorMessage(string error_reason)
	    {
	        MessageBox.Show($"Не удалось начать продажу: {error_reason}", "Не удалось начать продажу", MessageBoxButton.OK,
	            MessageBoxImage.Warning);
	    }

        //Получает список трейдов
	    protected override ICollection<Trade> GetTMItems()
	    {
	        return tmApi.GetTrades();
        }

        //Находит в базе предмет, соответствующий трейду
	    protected override Item GetDbItem(ItemsRepository repository, Trade api_item)
	    {
            return repository.GetById(api_item.i_classid, api_item.ui_real_instance);
        }

        //Получает цену предмета
	    protected override int GetItemMyPrice(Trade api_item)
	    {
	        return (int)api_item.ui_price*100;
	    }

        protected override bool GetItemNewPrice(TradeItemViewModel item, int tm_price, ref int myNewPrice)
        {
            /* Если минимальная цена меньше текущей - делаем нашу меньше минимальной на 
             * 1 коп.
             * 
             * Если минимальная - наша, то увеличиваем цену (до минимальной - 1коп) только
             * если разница больше заданных %
             */
            if ((tm_price < item.MyPrice) || (item.MyPrice < item.PriceLimit) || ((tm_price - item.MyPrice) / (float)tm_price > PriceThreshold))
            {
                myNewPrice = tm_price - 1;
                return true;
            }

            //Цену менять не надо
            myNewPrice = item.MyPrice;
            return false;
        }

        protected override int? GetItemTMPrice(TradeItemViewModel item)
        {
            return PriceCounter.GetMinSellPrice<TTMAPI>(item.ClassId, item.IntanceId, item.PriceLimit);
        }

        //Остановка
        /* Немного говнокод. У меня Stop
         * вызывается из ViewModel при уничтожении (закрытии окна)
         * но не факт, что это всегда так может быть.
         * 
         * На самом деле говнокод в том, что настройка находится
         * в воркере и к ней прибинжено окно */
	    public override void Stop()
	    {
	        base.Stop();
	        var settings = SettingsManager.LoadSettings();
	        settings.TradeMaxThreshold = PriceThreshold;
            SettingsManager.SaveSettings(settings);
	    }
    }
}