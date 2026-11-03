Console.WriteLine("Hello, World!");

// Programowanie strukturalne - tworzenie zmiennych i przypisywanie im wartości

// nr_albumu
// poziom wiedzy
// rok studiów

// 1 student
/*
string nr_albumu = "123456";
string poziom_wiedzy = "średnio zaawansowany";
int rok_studiów = 3;
*/

// Programowanie zorientowanie obiektowo (OOP - Object Oriented Programming) - tworzenie klas i obiektów

// Tworzymy obiekt klasy Student (instancję klasy Student) i przypisujemy wartości do jego właściwości
Student student1 = new Student
{
    NrAlbumu = "123456",
    PoziomWiedzy = "średnio zaawansowany",    
    RokStudiow = 3
};

// 2 student
Student student2 = new Student
{
    NrAlbumu = "123457",
    PoziomWiedzy = "zaawansowany",    
    RokStudiow = 3
};

student1.WyswietlInformacje();
student2.WyswietlInformacje();

// Tworzymy obiekt klasy Matrix (instancję klasy Matrix) i przypisujemy wartości do jego właściwości
Matrix m1 = new Matrix
{
    Rows = 2,
    Columns = 3,
    Data = new int[2, 3] 
    {
         { 1, 2, 3 }, 
         { 4, 5, 6 } 
    }
};

// Tworzymy drugi obiekt klasy Matrix (instancję klasy Matrix) i przypisujemy wartości do jego właściwości
Matrix m2 = new Matrix
{
    Rows = 3,
    Columns = 2,
    Data = new int[3, 2] 
    {
         { 7, 8 }, 
         { 9, 10 }, 
         { 11, 12 } 
    }
};

// Matrix m3 = m1 + m2; // Dodawanie macierzy m1 i m2

Console.WriteLine("Matrix m1:");

// Wywołanie metody DisplayMatrix() dla obiektu m1
m1.DisplayMatrix();

Console.WriteLine("Matrix m2:");

// Wywołanie metody DisplayMatrix() dla obiektu m2
m2.DisplayMatrix();


// Definicja klasy Student
class Student
{
    // Właściwości klasy Student (atrybuty obiektu)
    public string NrAlbumu { get; set; } 
    public string PoziomWiedzy { get; set; }    
    public int RokStudiow { get; set; }        
    
    // Metoda (funkcja) do wyświetlania informacji o studencie
    public void WyswietlInformacje()
    {
        Console.WriteLine($"Numer albumu: {NrAlbumu}");
        Console.WriteLine($"Poziom wiedzy: {PoziomWiedzy}");        
        Console.WriteLine($"Rok studiów: {RokStudiow}");
    }
}

class Miejsce
{
    public string Numer { get; set; }
    public Lokalizacja Lokalizacja { get; set; }
}

class Sala : Miejsce // Dziedziczenie, dzieiczymy po klasie Miejsce
{
    public int LiczbaMiejsc { get; set; }
}

// Klasa (Rzeczownik)
class EkspressDoKawy : Miejsce // Dziedziczenie, dzieiczymy po klasie Miejsce
{    
    public bool CzyDziala { get; set; }

    // Metoda (Czasownik)
    public void WlaczEkspress(string rodzajKawy)
    {
        if (CzyDziala)
        {
            Console.WriteLine($"Ekspres do kawy został włączony. Przygotowuje {rodzajKawy}.");
        }
        else
        {
            Console.WriteLine("Ekspres do kawy nie działa.");
        }
    }
}

class Budynek
{
    public string Nazwa { get; set; }
    public Adres Adres { get; set; }    
    public Sala[] Sale { get; set; }  // w którym mogą znajdować się sale
}

class Adres
{
    public string Ulica { get; set; }
    public string Miasto { get; set; }
    public string KodPocztowy { get; set; }
}

class Lokalizacja
{
    public int Pietro { get; set; }
    public float Dlugosc { get; set; }
    public float Szerokosc { get; set; }
}

// Klasa Matrix reprezentująca macierz
class Matrix
{
    public int Rows { get; set; }
    public int Columns { get; set; }
    public int[,] Data { get; set; }    

    public void DisplayMatrix()
    {
        for (int i = 0; i < Rows; i++)
        {
            for (int j = 0; j < Columns; j++)
            {
                Console.Write(Data[i, j] + " ");
            }
            Console.WriteLine();
        }
    }
}