﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Threading;
using WindowsCulturesViewer.Annotations;
using WindowsCulturesViewer.Models;

namespace WindowsCulturesViewer.ViewModels
{
    public class WCVViewModel : INotifyPropertyChanged
    {
        private readonly Dispatcher _uiDispatcher;
        private readonly List<CultureInfo> _cultures;

        private CultureInfo _currentCulture;

        private const double c_numExample = 124536.20;

        public ObservableCollection<Culture> Cultures { get; set; } = new ObservableCollection<Culture>();

        public string NumExample => c_numExample.ToString(_currentCulture);

        public string DateExample => DateTime.Now.ToString(_currentCulture);

        public string TimeExample => DateTime.Now.ToLocalTime().ToString(_currentCulture);

        public string CurrencyExample => c_numExample.ToString("C", _currentCulture);
        
        public WCVViewModel()
        {
            _uiDispatcher = Dispatcher.CurrentDispatcher;
            _cultures = CultureInfo.GetCultures(CultureTypes.AllCultures).ToList();
            SynchronizeCulturesAsunc();
        }

        public CultureInfo CurrentCulture
        {
            get => _currentCulture;
            set
            {
                if (Equals(_currentCulture, value))
                    return;

                _currentCulture = value;
                UpdateAll();
            }
        }

        private void GetNeutral(int level, Dictionary<Culture, bool> used, Culture parent = null)
        {
            switch (level)
            {
                case 1:
                    used.Keys.ToList().ForEach(culture =>
                    {
                        var cultureInfo = culture.CultureInfo;

                        if (!cultureInfo.IsNeutralCulture)
                            return;

                        var tag = cultureInfo.Name.Split('-');

                        if (tag.Length != level)
                            return;

                        _uiDispatcher.BeginInvoke(() => Cultures.Add(culture));
                        used[culture] = true;
                        GetNeutral(level + 1, used, culture);
                    });
                    break;
                default:
                    var subs = used.Keys.ToList().Where(culture =>
                    {
                        if (used[culture])
                            return false;

                        var cultureInfo = culture.CultureInfo;

                        if (!cultureInfo.IsNeutralCulture)
                            return false;

                        var tag = cultureInfo.Name.Split('-');

                        if (tag.Length != level)
                            return false;

                        if(!cultureInfo.Name.StartsWith(parent.CultureInfo.Name))
                            return false;

                        return true;
                    }).ToList();

                    if (!subs.Any())
                        break;

                    subs.ForEach(culture =>
                    {
                        _uiDispatcher.BeginInvoke(() => parent.SubCultures.Add(culture));
                        used[culture] = true;
                        GetNeutral(level + 1, used, culture);
                    });
                    break;
            }
        }

        private void GetNonNeutral(Dictionary<Culture, bool> used)
        {
            used.Keys.ToList().ForEach(culture =>
            {
                var cultureInfo = culture.CultureInfo;

                if(string.IsNullOrEmpty(cultureInfo.Name))
                    return;

                if (cultureInfo.IsNeutralCulture)
                    return;

                var par = used.Keys.ToList().AsParallel().AsOrdered().Where(info =>
                {
                    var i = info.CultureInfo;

                    if (!i.IsNeutralCulture)
                        return false;

                    if (!cultureInfo.Name.StartsWith(i.Name))
                        return false;

                    return true;
                }).ToList();

                var maxName = par.FirstOrDefault()?.CultureInfo.Name;

                if (string.IsNullOrEmpty(maxName))
                {
                    _uiDispatcher.BeginInvoke(() => Cultures.Add(culture));
                    used[culture] = true;
                    return;
                }

                par.ForEach(p =>
                {
                    if (p.CultureInfo.Name.CompareTo(maxName) == 1)
                        maxName = p.CultureInfo.Name;
                });

                var parent = used.Keys.ToList().Find(cul => cul.CultureInfo.Name == maxName);

                _uiDispatcher.BeginInvoke(() => parent?.SubCultures.Add(culture));
                used[culture] = true;
            });
        }

        private void SortCultures(List<Culture> cultures = null)
        {
            var list = cultures ?? Cultures.ToList();

            list.Sort((c1, c2) => string.CompareOrdinal(c1.CultureInfo.EnglishName, c2.CultureInfo.EnglishName));
            list.ForEach(culture =>
            {
                if (culture.SubCultures.Count > 1)
                {
                    var l = culture.SubCultures.ToList();
                    SortCultures(l);

                    _uiDispatcher.BeginInvoke(() =>
                    {
                        culture.SubCultures.Clear();
                        l.ForEach(c => culture.SubCultures.Add(c));
                    });
                }

            });

            if (cultures == null)
            {
                _uiDispatcher.BeginInvoke(() => 
                {
                    Cultures.Clear();
                    list.ForEach(culture => Cultures.Add(culture));
                });
            }
        }

        private void SynchronizeCultures()
        {
            Cultures.Clear();
            _cultures.Sort((c1, c2) => string.CompareOrdinal(c1.Name, c2.Name));

            var used = new Dictionary<Culture, bool>();
            _cultures.ForEach(c => used.Add(new Culture(c), false));

            GetNeutral(1, used);
            GetNonNeutral(used);
            SortCultures();

            UpdateAll();
        }

        private async void SynchronizeCulturesAsunc()
        {
            await Task.Run(SynchronizeCultures);
        }

        private void UpdateAll()
        {
            OnPropertyChanged(nameof(CurrentCulture));
            OnPropertyChanged(nameof(NumExample));
            OnPropertyChanged(nameof(DateExample));
            OnPropertyChanged(nameof(TimeExample));
            OnPropertyChanged(nameof(CurrencyExample));
        }
        
        #region Интерфейс уведомлений

        public event PropertyChangedEventHandler? PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }
}