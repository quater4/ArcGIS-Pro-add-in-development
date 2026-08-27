using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;

namespace XYL_Tools.Models
{
    internal class CheckOptions : INotifyPropertyChanged
    {
        // 私有成员
        private bool _checkNull = true;
        private bool _checkDuplicate = true;
        private bool _checkGeometry = true;
        private bool _checkSpatialReference = true;
        private bool _checkAllLayers = true;
        private bool _checkSelectedLayers = false;

        // 公共属性
        public bool CheckNull { get { return _checkNull; } set { _checkNull = value; OnPropertyChanged(); } }  // 是否检查空值
        public bool CheckDuplicate { get { return _checkDuplicate; } set { _checkDuplicate = value; OnPropertyChanged(); } } // 是否检查重复
        public bool CheckGeometry { get { return _checkGeometry; } set { _checkGeometry = value; OnPropertyChanged(); } } // 是否检查几何
        public bool CheckSpatialReference { get { return _checkSpatialReference; } set { _checkSpatialReference = value; OnPropertyChanged(); } } // 是否检查空间参考
        public bool CheckAllLayers 
        { 
            get 
            { return _checkAllLayers; 
            } 
            set 
            { 
                _checkAllLayers = value; 
                if(value) _checkSelectedLayers = false; 
                OnPropertyChanged(); 
                OnPropertyChanged(nameof(CheckSelectedLayers)); 
            } 
        } // 是否检查所有图层
        public bool CheckSelectedLayers 
        { 
            get 
            { 
                return _checkSelectedLayers; 
            } 
            set 
            { 
                _checkSelectedLayers = value; 
                if(value) _checkAllLayers = false;
                OnPropertyChanged(); 
                OnPropertyChanged(nameof(CheckAllLayers)); 
            } 
        } // 是否检查选中的图层

        // INotifyPropertyChanged implementation
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
