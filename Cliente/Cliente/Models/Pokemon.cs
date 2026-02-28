using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Cliente.Models
{
    //Klase honek Pokemon bat definitzen du eta bere propietate guztiak gordetzen ditu.
    //INotifyPropertyChanged interfazea erabiltzen du interfaze grafikoak eguneratzeko.
    public class Pokemon : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        private int _id;
        public int Id
        {
            get => _id;
            set => SetField(ref _id, value);
        }

        private string? _name;
        public string? Name
        {
            get => _name;
            set => SetField(ref _name, value);
        }

        private string? _thumbnail;
        public string? Thumbnail
        {
            get => _thumbnail;
            set => SetField(ref _thumbnail, value);
        }

        private int? _hp;
        public int? HP
        {
            get => _hp;
            set => SetField(ref _hp, value);
        }

        private int? _maxHP;
        public int? MaxHP
        {
            get => _maxHP;
            set => SetField(ref _maxHP, value);
        }

        private int? _attack;
        public int? Attack
        {
            get => _attack;
            set => SetField(ref _attack, value);
        }

        private int? _defense;
        public int? Defense
        {
            get => _defense;
            set => SetField(ref _defense, value);
        }

        private int? _spAttack;
        public int? SpAttack
        {
            get => _spAttack;
            set => SetField(ref _spAttack, value);
        }

        private int? _spDefense;
        public int? SpDefense
        {
            get => _spDefense;
            set => SetField(ref _spDefense, value);
        }

        private int? _speed;
        public int? Speed
        {
            get => _speed;
            set => SetField(ref _speed, value);
        }

        private string? _type;
        public string? Type
        {
            get => _type;
            set => SetField(ref _type, value);
        }

        private string? _move1;
        public string? Move1
        {
            get => _move1;
            set => SetField(ref _move1, value);
        }

        private string? _move2;
        public string? Move2
        {
            get => _move2;
            set => SetField(ref _move2, value);
        }

        private string? _move3;
        public string? Move3
        {
            get => _move3;
            set => SetField(ref _move3, value);
        }

        private string? _move4;
        public string? Move4
        {
            get => _move4;
            set => SetField(ref _move4, value);
        }

        private int? _move1Id;
        public int? Move1Id
        {
            get => _move1Id;
            set => SetField(ref _move1Id, value);
        }

        private int? _move2Id;
        public int? Move2Id
        {
            get => _move2Id;
            set => SetField(ref _move2Id, value);
        }

        private int? _move3Id;
        public int? Move3Id
        {
            get => _move3Id;
            set => SetField(ref _move3Id, value);
        }

        private int? _move4Id;
        public int? Move4Id
        {
            get => _move4Id;
            set => SetField(ref _move4Id, value);
        }

        private int? _move1Power;
        public int? Move1Power
        {
            get => _move1Power;
            set => SetField(ref _move1Power, value);
        }

        private int? _move2Power;
        public int? Move2Power
        {
            get => _move2Power;
            set => SetField(ref _move2Power, value);
        }

        private int? _move3Power;
        public int? Move3Power
        {
            get => _move3Power;
            set => SetField(ref _move3Power, value);
        }

        private int? _move4Power;
        public int? Move4Power
        {
            get => _move4Power;
            set => SetField(ref _move4Power, value);
        }

        private string? _move1Category;
        public string? Move1Category
        {
            get => _move1Category;
            set => SetField(ref _move1Category, value);
        }

        private string? _move2Category;
        public string? Move2Category
        {
            get => _move2Category;
            set => SetField(ref _move2Category, value);
        }

        private string? _move3Category;
        public string? Move3Category
        {
            get => _move3Category;
            set => SetField(ref _move3Category, value);
        }

        private string? _move4Category;
        public string? Move4Category
        {
            get => _move4Category;
            set => SetField(ref _move4Category, value);
        }

        private string? _move1Type;
        public string? Move1Type
        {
            get => _move1Type;
            set => SetField(ref _move1Type, value);
        }

        private string? _move2Type;
        public string? Move2Type
        {
            get => _move2Type;
            set => SetField(ref _move2Type, value);
        }

        private string? _move3Type;
        public string? Move3Type
        {
            get => _move3Type;
            set => SetField(ref _move3Type, value);
        }

        private string? _move4Type;
        public string? Move4Type
        {
            get => _move4Type;
            set => SetField(ref _move4Type, value);
        }
    }
}