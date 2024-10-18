using NeoConnectWS.Helper;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Data.SqlClient;
using System.Linq;
using System.Web;
using System.Web.Services;

namespace NeoConnectWS.WS
{
    /// <summary>
    /// Summary description for NeoConnect
    /// </summary>
    [WebService(Namespace = "http://tempuri.org/")]
    [WebServiceBinding(ConformsTo = WsiProfiles.BasicProfile1_1)]
    [System.ComponentModel.ToolboxItem(false)]
    // To allow this Web Service to be called from script, using ASP.NET AJAX, uncomment the following line. 
    // [System.Web.Script.Services.ScriptService]
    //public class NeoConnect : System.Web.Services.WebService
    //{

    //    [WebMethod]
    //    public string HelloWorld()
    //    {
    //        return "Hello World";
    //    }
    //}

    public class NeoConnect : System.Web.Services.WebService
    {
        SqlConnection con;

        [WebMethod]
        public TVMKullaniciBilgi KullaniciKontrol(string email, string sifre, string clientIp, string macAdresi)
        {
            TVMKullaniciBilgi detay = new TVMKullaniciBilgi();

            string hashedPassword = Encryption.HashPassword(sifre);
            try
            {
                con = new SqlConnection(this.ConnectionString());
                SqlCommand com = new SqlCommand("select * from TvmKullanicilar where email='" + email + "' and sifre='" + hashedPassword + "'", con);
                con.Open();
                bool kullaniciVarMi = false;
                SqlDataReader dr = com.ExecuteReader();
                while (dr.Read())
                {
                    detay = new TVMKullaniciBilgi();
                    detay.TvmKodu = Convert.ToInt32(dr["TVMKodu"].ToString());
                    if (detay.TvmKodu.ToString().Contains("149"))
                    {
                        detay.hataMesaji = String.Empty;
                        detay.hataMesaji = "Yetkisiz ip girişi. Lütfen yöneticinize başvurunuz.";
                    }

                    detay.KullaniciKodu = Convert.ToInt32(dr["KullaniciKodu"].ToString());
                    detay.CepTel = dr["CepTelefon"].ToString();
                    detay.Adi = dr["Adi"].ToString();
                    detay.Soyadi = dr["Soyadi"].ToString();
                    detay.durum = Convert.ToInt32(dr["Durum"]);
                    //detay.WebAdresi = dr["WebAdresi"].ToString();
                    //detay.Logo= dr["Logo"].ToString();

                    kullaniciVarMi = true;
                }
                dr.Close();
                con.Close();
                if (kullaniciVarMi)
                {
                    var tvmDetay = GetDetay(detay.TvmKodu);
                    var aktifTVMKodu = tvmDetay.BagliOlduguTvmKodu;
                    bool MacAdresiKontrol = false;
                    if (tvmDetay != null)
                    {
                        if (aktifTVMKodu == -9999)
                        {
                            if (tvmDetay.IpmiMacmi == "Mac")
                            {
                                MacAdresiKontrol = true;
                            }
                        }
                        else
                        {
                            var tDetay = GetDetay(aktifTVMKodu);
                            if (tDetay.IpmiMacmi == "Mac")
                            {
                                MacAdresiKontrol = true;
                            }
                        }
                        if (!String.IsNullOrEmpty(tvmDetay.HataMesaji))
                        {
                            detay.hataMesaji = tvmDetay.HataMesaji;
                        }

                    }
                    if (MacAdresiKontrol)
                    {
                        detay.yetkiliKullanici = this.YetkiKontrol(detay.TvmKodu, "", macAdresi);
                    }
                    else
                    {
                        detay.yetkiliKullanici = this.YetkiKontrol(detay.TvmKodu, clientIp, "");
                    }
                    //if (!detay.yetkiliKullanici)
                    //{
                    //    detay.hataMesaji = String.Empty;
                    //    detay.hataMesaji = "Yetkisiz ip girişi. Lütfen yöneticinize başvurunuz.";
                    //}
                }
                else
                {
                    detay.hataMesaji = String.Empty;
                    detay.hataMesaji = "Kullanıcı adı veya şifre hatalı girilmiştir.";
                }

            }
            catch (Exception ex)
            {
                detay.hataMesaji = ex.Message;
                throw;
            }

            return detay;
        }

        [WebMethod]
        public bool YetkiKontrol(int tvmKodu, string clientIp, string mac)
        {
            bool yetkiliMi = false;
            try
            {
                TVMIPBaglanti baglanti = new TVMIPBaglanti();
                List<TVMIPBaglanti> baglantiList = new List<TVMIPBaglanti>();
                string baslangicIp = "";
                if (!String.IsNullOrEmpty(clientIp))
                {
                    baslangicIp = clientIp;
                }
                else
                {
                    baslangicIp = mac;
                }
                con = new SqlConnection(this.ConnectionString());
                SqlCommand com = new SqlCommand("select * from TVMIPBaglanti where TVMKodu=" + tvmKodu + " and BaslangicIP ='" + baslangicIp + "'", con);
                con.Open();
                SqlDataReader dr = com.ExecuteReader();
                while (dr.Read())
                {
                    baglanti = new TVMIPBaglanti();
                    baglanti.TVMKodu = Convert.ToInt32(dr["TVMKodu"].ToString());
                    baglanti.BaslangicIP = dr["BaslangicIP"].ToString();
                    baglanti.BitisIP = dr["BitisIP"].ToString();
                    baglantiList.Add(baglanti);
                }
                dr.Close();
                con.Close();
                if (baglantiList.Count > 0)
                {
                    foreach (var item in baglantiList)
                    {
                        if (!String.IsNullOrEmpty(clientIp))
                        {
                            if (clientIp == item.BaslangicIP)
                            {
                                yetkiliMi = true;
                            }
                        }
                        else if (!String.IsNullOrEmpty(mac))
                        {
                            if (mac == item.BaslangicIP)
                            {
                                yetkiliMi = true;
                            }
                        }
                    }
                }
                if (!String.IsNullOrEmpty(mac))
                {
                    if (!yetkiliMi)
                    {
                        var kayitTarihi = Convert.ToDateTime(TurkeyDateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                        var macAdresEkle = this.NeoConnectMacAddressAdd(tvmKodu, mac, mac, kayitTarihi, 1);
                        if (String.IsNullOrEmpty(macAdresEkle.HataMesaji))
                        {
                            yetkiliMi = true;
                        }
                        else
                        {
                            yetkiliMi = false;
                        }
                    }
                }
            }
            catch (Exception)
            {
                throw;
            }
            return yetkiliMi;
        }

        [WebMethod]
        public TVMDetayBilgi GetDetay(int tvmKodu)
        {
            TVMDetayBilgi bilgi = new TVMDetayBilgi();
            try
            {
                con = new SqlConnection(this.ConnectionString());
                SqlCommand com = new SqlCommand("select * from TVMDetay where Kodu=" + tvmKodu, con);
                con.Open();
                SqlDataReader dr = com.ExecuteReader();
                while (dr.Read())
                {
                    bilgi.TvmKodu = Convert.ToInt32(dr["Kodu"].ToString());
                    bilgi.BagliOlduguTvmKodu = Convert.ToInt32(dr["BagliOlduguTVMKodu"].ToString());
                    bilgi.MobilOnay = dr["MobilDogrulama"].ToString();
                    bilgi.IpmiMacmi = dr["IpmiMacmi"].ToString();

                    if (dr["Durum"].ToString() == "0")
                    {
                        bilgi.HataMesaji = "Satış kanalınız devre dışı bırakılmıştır. Lütfen yöneticinizle iletişime geçiniz.";
                    }
                }
                dr.Close();
                con.Close();
            }
            catch (Exception ex)
            {
                bilgi.HataMesaji = ex.Message;
                throw;
            }
            return bilgi;

        }

        [WebMethod]
        public TVMBilgi GetTVMWebServisBilgileri(int tvmKodu, int sirketKodu)
        {
            SqlConnection con;
            TVMBilgi bilgi = new TVMBilgi();
            con = new SqlConnection(this.ConnectionString()); //komutumuzun bağlantısını öğreniyoruz.
            var tvmDetay = this.GetDetay(tvmKodu);
            bool merkezAcentemi = false;
            int merkezAcenteKodu = 0;
            if (tvmDetay.TvmKodu != null)
            {
                if (tvmDetay.BagliOlduguTvmKodu != -9999)
                {
                    tvmKodu = tvmDetay.TvmKodu;
                    merkezAcenteKodu = tvmDetay.BagliOlduguTvmKodu;
                    merkezAcentemi = false;
                }
                else
                {
                    merkezAcentemi = true;
                }
            }

            SqlCommand com;
            if (merkezAcentemi)
            { //  ("select * from OtoLoginSigortaSirketKullanicilar where (TVMKodu=" + tvmKodu + " or AltTVMKodu=" + tvmKodu + ")
                com = new SqlCommand("select * from OtoLoginSigortaSirketKullanicilar where TVMKodu = " + tvmKodu + " and AltTVMKodu is null and TUMKodu = " + sirketKodu, con);
            }
            else
            {
                com = new SqlCommand("select * from OtoLoginSigortaSirketKullanicilar where ((GrupKodu is null  and TVMKodu = " + merkezAcenteKodu + ") or (GrupKodu is not null and AltTVMKodu = " + tvmKodu + ")) and TUMKodu=" + sirketKodu, con);

            }
            con.Open();
            SqlDataReader dr = com.ExecuteReader();
            while (dr.Read())
            {
                bilgi.KullaniciAdi = Sifreleme.Encrypt(dr["KullaniciAdi"].ToString(), "!082017?");
                bilgi.AcenteKodu = dr["AcenteKodu"].ToString();
                bilgi.Sifre = Sifreleme.Encrypt(dr["Sifre"].ToString(), "!082017?");
                string grupKodu = dr["GrupKodu"].ToString();
                bilgi.SmsKodTelNo = Sifreleme.Encrypt(dr["SmsKodTelNo"].ToString(), "!082017?");
                bilgi.SmsKodSecretKey1 = Sifreleme.Encrypt(dr["SmsKodSecretKey1"].ToString(), "!082017?");
                bilgi.InputTextKullaniciId = Sifreleme.Encrypt(dr["InputTextKullaniciId"].ToString(), "!082017?");
                bilgi.InputTextSifreId = Sifreleme.Encrypt(dr["InputTextSifreId"].ToString(), "!082017?");
                bilgi.InputTextGirisId = Sifreleme.Encrypt(dr["InputTextGirisId"].ToString(), "!082017?");
                if (!String.IsNullOrEmpty(grupKodu))
                {
                    dr.Close();
                    con.Close();
                    con.Open();

                    com = new SqlCommand("select * from NeoConnectSirketGrupKullaniciDetay where GrupKodu =" + grupKodu + " and TVMKodu  =" + merkezAcenteKodu, con);

                    SqlDataReader grupReader = com.ExecuteReader();
                    while (grupReader.Read())
                    {
                        bilgi = new TVMBilgi();
                        bilgi.KullaniciAdi = Sifreleme.Encrypt(grupReader["KullaniciAdi"].ToString(), "!082017?");
                        bilgi.Sifre = Sifreleme.Encrypt(grupReader["Sifre"].ToString(), "!082017?");
                        bilgi.GrupKodu = Convert.ToInt32(grupReader["GrupKodu"].ToString());
                        bilgi.SmsKodTelNo = Sifreleme.Encrypt(grupReader["SmsKodTelNo"].ToString(), "!082017?");
                        bilgi.SmsKodSecretKey1 = Sifreleme.Encrypt(grupReader["SmsKodSecretKey1"].ToString(), "!082017?");
                        bilgi.SmsKodSecretKey2 = Sifreleme.Encrypt(grupReader["SmsKodSecretKey2"].ToString(), "!082017?");
                        bilgi.InputTextKullaniciId = Sifreleme.Encrypt(dr["InputTextKullaniciId"].ToString(), "!082017?");
                        bilgi.InputTextSifreId = Sifreleme.Encrypt(dr["InputTextSifreId"].ToString(), "!082017?");
                        bilgi.InputTextGirisId = Sifreleme.Encrypt(dr["InputTextGirisId"].ToString(), "!082017?");
                    }
                    grupReader.Close();
                    con.Close();
                    break;
                }
            }
            dr.Close();
            con.Close();
            return bilgi;
        }

        [WebMethod]
        public NeoConnectSirketDetay GetNeoConnectSirketDetay(int sirketKodu)
        {
            NeoConnectSirketDetay bilgi = new NeoConnectSirketDetay();
            try
            {
                con = new SqlConnection(this.ConnectionString());

                SqlCommand com = new SqlCommand("select * from NeoConnectSirketDetay where TUMKodu=" + sirketKodu, con);
                con.Open();
                SqlDataReader dr = com.ExecuteReader();
                while (dr.Read())
                {
                    bilgi.TUMKodu = Convert.ToInt32(dr["TUMKodu"].ToString());
                    bilgi.InputTextKullaniciId = dr["InputTextKullaniciId"].ToString();
                    bilgi.InputTextAcenteKoduId = dr["InputTextAcenteKoduId"].ToString();
                    bilgi.InputTextSifreId = dr["InputTextSifreId"].ToString();
                    bilgi.InputTextGirisId = dr["InputTextGirisId"].ToString();
                    if (!String.IsNullOrEmpty(dr["LoginUrl"].ToString()))
                    {
                        bilgi.LoginUrl = Sifreleme.Encrypt(dr["LoginUrl"].ToString(), "!082017?");

                    }

                }
                dr.Close();
                con.Close();
            }
            catch (Exception ex)
            {
                bilgi.HataMesaji = ex.ToString();
                throw;
            }

            return bilgi;

        }

        [WebMethod]
        public SirketURL GetSigortaSirketURL(int tvmKodu, int sirketKodu)
        {
            SirketURL uri = new SirketURL();
            try
            {
                con = new SqlConnection(this.ConnectionString());
                SqlCommand com;
                var tvmDetay = this.GetDetay(tvmKodu);
                bool merkezAcentemi = true;
                int merkezAcenteKodu = 0;
                if (tvmDetay.TvmKodu != null)
                {
                    if (tvmDetay.BagliOlduguTvmKodu != -9999)
                    {
                        tvmKodu = tvmDetay.TvmKodu;
                        merkezAcenteKodu = tvmDetay.BagliOlduguTvmKodu;
                        merkezAcentemi = false;
                    }
                }
                if (merkezAcentemi)
                {
                    com = new SqlCommand("select * from OtoLoginSigortaSirketKullanicilar where TVMKodu=" + tvmKodu + " and AltTVMKodu is null and TUMKodu=" + sirketKodu, con);
                }
                else
                {
                    com = new SqlCommand("select * from OtoLoginSigortaSirketKullanicilar where ((GrupKodu is null  and TVMKodu  = " + merkezAcenteKodu + ") or (GrupKodu is not null and AltTVMKodu = " + tvmKodu + ")) and TUMKodu=" + sirketKodu, con);
                }
                con.Open();
                SqlDataReader dr = com.ExecuteReader();
                while (dr.Read())
                {
                    uri.URL = Sifreleme.Encrypt(dr["LoginUrl"].ToString(), "!082017?");
                    uri.proxyIpPort = Sifreleme.Encrypt(dr["ProxyIpPort"].ToString(), "!082017?");
                    uri.proxyKullaniciAdi = Sifreleme.Encrypt(dr["ProxyKullaniciAdi"].ToString(), "!082017?");
                    uri.proxySifre = Sifreleme.Encrypt(dr["ProxySifre"].ToString(), "!082017?");
                }
                dr.Close();
                con.Close();
            }
            catch (Exception ex)
            {
                uri.HataMesaji = ex.ToString();
                throw;
            }
            return uri;
        }

        [WebMethod]
        public List<NeoConnectTvmYetki> GetNeoConnectTvmSirket(int tvmKodu)
        {
            NeoConnectTvmYetki yetki = new NeoConnectTvmYetki();
            List<NeoConnectTvmYetki> strList = new List<NeoConnectTvmYetki>();
            try
            {
                con = new SqlConnection(this.ConnectionString());

                //SqlCommand com = new SqlCommand("select * from NeoConnectTvmSirketYetkileri where TvmKodu = " + tvmKodu, con);
                SqlCommand com = new SqlCommand("select * from NeoConnectTvmSirketYetkileri where TvmKodu = " + tvmKodu, con);
                con.Open();
                SqlDataReader dr = com.ExecuteReader();
                while (dr.Read())
                {
                    yetki = new NeoConnectTvmYetki();
                    yetki.TumKodu = dr["TumKodu"].ToString();
                    yetki.TvmKodu = Convert.ToInt32(dr["TvmKodu"]);
                    if (int.TryParse(dr["TumKodu2"].ToString(), out int temptum))
                        yetki.TumKodu2 = temptum;
                    strList.Add(yetki);
                }
                dr.Close();
                con.Close();
            }
            catch (Exception ex)
            {
                yetki.HataMesaji = ex.ToString();
                throw;
            }
            return strList;
        }
        [WebMethod]
        public List<TumDetay> GetTumYetkiList(int tvmKodu)
        {
            TumDetay yetki = new TumDetay();
            List<TumDetay> strList = new List<TumDetay>();
            try
            {
                con = new SqlConnection(this.ConnectionString());

                SqlCommand com = new SqlCommand("select distinct t.* from  TUMDetay t left join NeoConnectTvmSirketYetkileri  n on t.BirlikKodu=n.TumKodu where TvmKodu=" + tvmKodu, con);
                con.Open();
                SqlDataReader dr = com.ExecuteReader();
                while (dr.Read())
                {
                    yetki = new TumDetay();
                    yetki.TumKodu = int.Parse(dr["Kodu"].ToString());
                    yetki.Unvani = dr["Unvani"].ToString();
                    strList.Add(yetki);
                }
                dr.Close();
                con.Close();
            }
            catch (Exception ex)
            {
            }
            return strList;
        }
        [WebMethod]
        public List<TvmDetay> GetTvmDetay()
        {
            TvmDetay yetki;
            List<TvmDetay> strList = new List<TvmDetay>();
            try
            {
                con = new SqlConnection(this.ConnectionString());

                SqlCommand com = new SqlCommand("select kodu,webadresi,logo,unvani from tvmdetay", con);
                con.Open();
                SqlDataReader dr = com.ExecuteReader();
                while (dr.Read())
                {
                    yetki = new TvmDetay();
                    yetki.TvmKodu = int.Parse(dr["Kodu"].ToString());
                    yetki.Unvani = dr["Unvani"].ToString();
                    yetki.WebAdresi = dr["WebAdresi"].ToString();
                    yetki.Logo = dr["Logo"].ToString();
                    strList.Add(yetki);
                }
                dr.Close();
                con.Close();
            }
            catch (Exception ex)
            {
            }
            return strList;
        }

        [WebMethod]
        public List<NeoConnectSirketDetay> GetTumList()
        {
            NeoConnectSirketDetay yetki = new NeoConnectSirketDetay();
            List<NeoConnectSirketDetay> strList = new List<NeoConnectSirketDetay>();
            try
            {
                con = new SqlConnection(this.ConnectionString());

                SqlCommand com = new SqlCommand("select * from NeoConnectSirketDetay", con);
                con.Open();
                SqlDataReader grupReader = com.ExecuteReader();
                while (grupReader.Read())
                {
                    yetki = new NeoConnectSirketDetay();
                    yetki.InputTextKullaniciId = grupReader["InputTextKullaniciId"].ToString();
                    yetki.InputTextSifreId = grupReader["InputTextSifreId"].ToString();
                    yetki.InputTextGirisId = grupReader["InputTextGirisId"].ToString();
                    yetki.TextTUMKodu = Sifreleme.Encrypt(grupReader["TUMKodu"].ToString(), "!082017?");
                    yetki.LoginUrl = grupReader["LoginUrl"].ToString();
                    yetki.SigortaSirketAdi = grupReader["SigortaSirketAdi"].ToString();
                    strList.Add(yetki);
                }
                grupReader.Close();
                con.Close();
            }
            catch (Exception ex)
            {
                yetki.HataMesaji = ex.ToString();
                throw;
            }
            return strList;
        }
        [WebMethod]
        public List<NeoConnectSirketDetay> GetLoginTumList(int tvmkodu)
        {
            NeoConnectSirketDetay yetki = new NeoConnectSirketDetay();
            List<NeoConnectSirketDetay> strList = new List<NeoConnectSirketDetay>();
            try
            {
                con = new SqlConnection(this.ConnectionString());

                SqlCommand com = new SqlCommand("select * from OtoLoginSigortaSirketKullanicilar where TVMKodu=" + tvmkodu, con);
                con.Open();
                SqlDataReader grupReader = com.ExecuteReader();
                while (grupReader.Read())
                {
                    yetki = new NeoConnectSirketDetay();
                    yetki.TextTUMKodu = Sifreleme.Encrypt(grupReader["TUMKodu"].ToString(), "!082017?");
                    yetki.SigortaSirketAdi = grupReader["SigortaSirketAdi"].ToString();
                    strList.Add(yetki);
                }
                grupReader.Close();
                con.Close();
            }
            catch (Exception ex)
            {
                yetki.HataMesaji = ex.ToString();
                throw;
            }
            return strList;
        }
        [WebMethod]
        public TVMSirketProxy GetSigortaSirketProxy(int tvmKodu, int sirketKodu)
        {
            TVMSirketProxy proxy = new TVMSirketProxy();
            try
            {
                con = new SqlConnection(this.ConnectionString());
                var tvmDetay = this.GetDetay(tvmKodu);
                if (tvmDetay.TvmKodu != null)
                {
                    if (tvmDetay.BagliOlduguTvmKodu != -9999)
                    {
                        tvmKodu = tvmDetay.BagliOlduguTvmKodu;
                    }
                }
                SqlCommand com = new SqlCommand("select * from OtoLoginSigortaSirketKullanicilar where TVMKodu=" + tvmKodu + " and TUMKodu=" + sirketKodu, con);
                con.Open();
                SqlDataReader dr = com.ExecuteReader();
                while (dr.Read())
                {
                    proxy.proxyIpPort = Sifreleme.Encrypt(dr["ProxyIpPort"].ToString(), "!082017?");
                }
                dr.Close();
                con.Close();
            }
            catch (Exception ex)
            {
                proxy.HataMesaji = ex.ToString();
                throw;
            }
            return proxy;
        }

        [WebMethod]
        public NeoConnectLog GetNeoConnectLog(int tvmKodu, int KullaniciKodu, string KullaniciAdi, DateTime kullaniciGirisTarihi)
        {
            NeoConnectLog log = new NeoConnectLog();
            try
            {
                con = new SqlConnection(this.ConnectionString()); //komutumuzun bağlantısını öğreniyoruz.
                DateTime time = new DateTime();
                time = TurkeyDateTime.Now;

                var gun = time.Year + time.Month + time.Day + time.Hour + time.Minute;

                SqlCommand com = new SqlCommand("insert into  NeoConnectLog(TvmKodu,KullaniciKodu,Kullanici,KullaniciGirisTarihi) VALUES(" + tvmKodu + "," + KullaniciKodu + ",'" + KullaniciAdi.ToString() + "', @DATE )", con);
                com.Parameters.AddWithValue("@DATE", TurkeyDateTime.Now);
                con.Open();
                SqlDataReader dr = com.ExecuteReader();

                dr.Close();
                con.Close();
            }
            catch (Exception ex)
            {
                log.HataMesaji = ex.ToString();
                throw;
            }
            return log;
        }

        [WebMethod]
        public NeoConnectLog GetNeoConnectSirketLog(int tvmKodu, int KullaniciKodu, string KullaniciAdi, DateTime kullaniciGirisTarihi, string SigortaSirketKodu, string IpAdresi, string MACAdresi)
        {
            NeoConnectLog log = new NeoConnectLog();
            try
            {
                con = new SqlConnection(this.ConnectionString()); //komutumuzun bağlantısını öğreniyoruz.
                DateTime time = new DateTime();
                time = TurkeyDateTime.Now;

                var gun = time.Year + time.Month + time.Day + time.Hour + time.Minute;
                SqlCommand com = new SqlCommand("insert into  NeoConnectLog(TvmKodu,KullaniciKodu,Kullanici,KullaniciGirisTarihi,SigortaSirketKodu,IPAdresi,MacAdresi) VALUES(" + tvmKodu + "," + KullaniciKodu + ",'" + KullaniciAdi.ToString() + "', @DATE ,'" + SigortaSirketKodu + "','" + IpAdresi + "','" + MACAdresi.ToString() + "')", con);
                com.Parameters.AddWithValue("@DATE", TurkeyDateTime.Now);
                con.Open();
                SqlDataReader dr = com.ExecuteReader();

                dr.Close();
                con.Close();
            }
            catch (Exception ex)
            {
                log.HataMesaji = ex.ToString();
                throw;
            }
            return log;
        }
        [WebMethod]
        public NeoConnectLog GetNeoConnectSirketGrupSirketLog(int tvmKodu, int KullaniciKodu, string KullaniciAdi, DateTime kullaniciGirisTarihi, string SigortaSirketKodu, string IpAdresi, string MACAdresi, string SirketKullaniciAdi, string SirketKullaniciSifresi, int? GrupKodu)
        {
            //TeklifNo: GrupAdi
            //poliçeno: GrupKullaniciAdi
            NeoConnectLog log = new NeoConnectLog();
            try
            {
                con = new SqlConnection(this.ConnectionString()); //komutumuzun bağlantısını öğreniyoruz.
                DateTime time = new DateTime();
                time = TurkeyDateTime.Now;

                var gun = time.Year + time.Month + time.Day + time.Hour + time.Minute;
                SqlCommand com = new SqlCommand("insert into  NeoConnectLog(TvmKodu,KullaniciKodu,Kullanici,KullaniciGirisTarihi,SigortaSirketKodu,IPAdresi,MacAdresi,SirketKullaniciAdi,SirketKullaniciSifresi,GrupKodu) VALUES(" + tvmKodu + "," + KullaniciKodu + ",'" + KullaniciAdi.ToString() + "', @DATE ,'" + SigortaSirketKodu + "','" + IpAdresi + "','" + MACAdresi.ToString() + "','" + SirketKullaniciAdi + "','" + SirketKullaniciSifresi + "'," + GrupKodu + ")", con);
                com.Parameters.AddWithValue("@DATE", TurkeyDateTime.Now);
                con.Open();
                SqlDataReader dr = com.ExecuteReader();

                dr.Close();
                con.Close();
            }
            catch (Exception ex)
            {
                log.HataMesaji = ex.ToString();
                throw;
            }
            return log;
        }

        [WebMethod]
        public TVMIPBaglanti NeoConnectMacAddressAdd(int tvmKodu, string baslangicIP, string bitisIP, DateTime kayitTarihi, byte durum)
        {
            TVMIPBaglanti mac = new TVMIPBaglanti();
            NeoConnect client = new NeoConnect();
            try
            {
                con = new SqlConnection(this.ConnectionString()); //komutumuzun bağlantısını öğreniyoruz.
                DateTime time = new DateTime();
                time = TurkeyDateTime.Now;
                var gun = Convert.ToDateTime(TurkeyDateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));


                SqlCommand kayitVarmi = new SqlCommand("select  *  from  TVMIPBaglanti  where  tvmkodu = " + tvmKodu + " and  BaslangicIP = '" + baslangicIP + "' ", con);
                con.Open();
                SqlDataReader drKayitVarmi = kayitVarmi.ExecuteReader();
                bool macKayitlimi = false;
                while (drKayitVarmi.Read())
                {
                    macKayitlimi = true;
                }
                drKayitVarmi.Close();
                con.Close();
                if (!macKayitlimi)
                {
                    SqlCommand siraNoCom = new SqlCommand("select  top 1  SiraNo  from  TVMIPBaglanti  order  by  SiraNo  desc ", con);
                    con.Open();
                    SqlDataReader drr = siraNoCom.ExecuteReader();
                    while (drr.Read())
                    {
                        mac.SiraNo = Convert.ToInt32(drr["SiraNo"]) + 1;
                    }
                    drr.Close();
                    con.Close();

                    con.Open();
                    SqlCommand com = new SqlCommand("insert into TVMIPBaglanti(tvmKodu,SiraNo, baslangicIP, bitisIP, kayitTarihi,durum) VALUES(" + tvmKodu + "," + mac.SiraNo + ",'" + baslangicIP.ToString() + "','" + bitisIP.ToString() + "',@DATE," + durum + ")", con);
                    com.Parameters.AddWithValue("@DATE", DateTime.Now);
                    SqlDataReader dr = com.ExecuteReader();
                    dr.Close();
                    con.Close();
                }


            }
            catch (Exception ex)
            {
                mac.HataMesaji = ex.ToString();
                throw;
            }
            return mac;
        }

        [WebMethod]
        public NeoConnectLog GetNeoConnectLogCikis(int tvmKodu, int KullaniciKodu)
        {
            NeoConnectLog logCikis = new NeoConnectLog();

            try
            {
                con = new SqlConnection(this.ConnectionString()); //komutumuzun bağlantısını öğreniyoruz.
                DateTime time = new DateTime();
                time = TurkeyDateTime.Now;

                var gun = time.Year + time.Month + time.Day + time.Hour + time.Minute;

                SqlCommand com = new SqlCommand("select max(KullaniciGirisTarihi), LogId from NeoConnectLog where tvmkodu = " + tvmKodu + " and KullaniciKodu = " + KullaniciKodu + " group by LogId", con);
                con.Open();
                SqlDataReader dr = com.ExecuteReader();
                while (dr.Read())
                {
                    logCikis.LogId = Convert.ToInt32(dr["LogId"].ToString());

                }
                dr.Close();
                con.Close();

                com = new SqlCommand("Update NeoConnectLog set KullaniciCikisTarihi = @DATE where LogId=" + logCikis.LogId, con);
                com.Parameters.AddWithValue("@DATE", TurkeyDateTime.Now);
                con.Open();
                SqlDataReader drCikis = com.ExecuteReader();
            }
            catch (Exception ex)
            {
                logCikis.HataMesaji = ex.ToString();
                throw;
            }


            return logCikis;
        }

        [WebMethod]
        public NeoConnectLog GetNeoConnectSirketLogCikis(int tvmKodu, int KullaniciKodu, string SigortaSirketKodu)
        {
            NeoConnectLog logCikis = new NeoConnectLog();

            try
            {
                con = new SqlConnection(this.ConnectionString()); //komutumuzun bağlantısını öğreniyoruz.
                DateTime time = new DateTime();
                time = TurkeyDateTime.Now;

                var gun = time.Year + time.Month + time.Day + time.Hour + time.Minute;

                SqlCommand com = new SqlCommand("select max(KullaniciGirisTarihi), LogId from NeoConnectLog where tvmkodu = " + tvmKodu + " and KullaniciKodu = " + KullaniciKodu + " and SigortaSirketKodu ='" + SigortaSirketKodu + "' group by LogId", con);
                con.Open();
                SqlDataReader dr = com.ExecuteReader();
                bool KayitVarmi = false;
                while (dr.Read())
                {
                    logCikis.LogId = Convert.ToInt32(dr["LogId"].ToString());
                    KayitVarmi = true;
                }
                dr.Close();
                con.Close();
                if (KayitVarmi)
                {
                    com = new SqlCommand("Update NeoConnectLog set KullaniciCikisTarihi = @DATE where LogId=" + logCikis.LogId, con);
                    com.Parameters.AddWithValue("@DATE", TurkeyDateTime.Now);
                    con.Open();
                    SqlDataReader drCikis = com.ExecuteReader();
                    drCikis.Close();
                    con.Close();
                }

            }
            catch (Exception ex)
            {
                logCikis.HataMesaji = ex.ToString();
                throw;
            }


            return logCikis;
        }

        [WebMethod]
        public NeoConnetVersiyonNo GetNeoConnetVersiyonNo()
        {
            NeoConnetVersiyonNo Versiyon = new NeoConnetVersiyonNo();
            try
            {
                con = new SqlConnection(this.ConnectionString());

                SqlCommand com = new SqlCommand("select * from NeoConnetVersiyonNo ", con);

                con.Open();
                SqlDataReader dr = com.ExecuteReader();
                while (dr.Read())
                {
                    Versiyon.VersiyonNo = Convert.ToInt32(dr["VersiyonNo"].ToString());
                }
                dr.Close();
                con.Close();
            }
            catch (Exception ex)
            {
                Versiyon.HataMesaji = ex.ToString();
                throw;
            }

            return Versiyon;
        }

        [WebMethod]
        public List<NeoConnectYasakliUrller> GetYasakliUrl(int tvmKodu, int altTvmKodu, int sirketKodu)
        {
            List<NeoConnectYasakliUrller> yasakliUrlList = new List<NeoConnectYasakliUrller>();
            con = new SqlConnection(this.ConnectionString());
            SqlCommand com = new SqlCommand("select * from NeoConnectYasakliUrller where TvmKodu = " + tvmKodu + " and AltTvmKodu = " + altTvmKodu + " and SigortaSirketKodu = " + sirketKodu, con);
            con.Open();
            SqlDataReader dr = com.ExecuteReader();
            NeoConnectYasakliUrller yasakliUrl = new NeoConnectYasakliUrller();
            while (dr.Read())
            {
                try
                {
                    yasakliUrl = new NeoConnectYasakliUrller();
                    yasakliUrl.id = Convert.ToInt32(dr["id"].ToString());
                    yasakliUrl.TvmKodu = Convert.ToInt32(dr["TvmKodu"].ToString());
                    yasakliUrl.AltTvmKodu = Convert.ToInt32(dr["AltTvmKodu"].ToString());
                    yasakliUrl.SigortaSirketKodu = Convert.ToInt32(dr["SigortaSirketKodu"].ToString());

                    yasakliUrl.YasaklanacakUrlleri = dr["YasaklanacakUrl"].ToString();
                    yasakliUrlList.Add(yasakliUrl);
                }
                catch (Exception ex)
                {
                    yasakliUrl.HataMesaji = ex.ToString();
                    throw;
                }
            }
            dr.Close();
            con.Close();
            return yasakliUrlList;
        }


        [WebMethod]
        public NeoConnectSMSKullaniciBilgi GetSmsKullaniciBilgileri(int tvmKodu)
        {
            NeoConnectSMSKullaniciBilgi smsBilgi = new NeoConnectSMSKullaniciBilgi();

            try
            {
                SqlConnection con;
                con = new SqlConnection(this.ConnectionString());
                SqlCommand com = new SqlCommand("select * from TVMSMSKullaniciBilgi where TVMKodu = " + tvmKodu, con);
                con.Open();
                SqlDataReader dr = com.ExecuteReader();
                while (dr.Read())
                {
                    smsBilgi.TvmKodu = Convert.ToInt32(dr["TVMKodu"].ToString());
                    smsBilgi.KullaniciAdi = dr["KullaniciAdi"].ToString();
                    smsBilgi.Sifre = dr["Sifre"].ToString();
                    smsBilgi.Gonderen = dr["Gonderen"].ToString();
                    smsBilgi.SmsSuresi = Convert.ToInt16(dr["SmsSuresiDK"].ToString());
                }
                dr.Close();
                con.Close();
            }
            catch (Exception ex)
            {
                smsBilgi.HataMesaji = ex.Message;
                throw;
            }

            return smsBilgi;
        }

        [WebMethod]
        public bool KullaniciMobilOnayKoduEkle(int tvmKodu, int kullanicikodu, string mobilOnayKodu)
        {
            bool guncellendi = false;
            try
            {
                TVMKullaniciBilgi bilgi = new TVMKullaniciBilgi();
                SqlConnection con;
                con = new SqlConnection(this.ConnectionString());
                SqlCommand com = new SqlCommand("update TvmKullanicilar set MobilDogrulamaKodu = '" + mobilOnayKodu + "' where TVMKodu = " + tvmKodu + " and KullaniciKodu = " + kullanicikodu, con);
                con.Open();
                com.ExecuteReader();
                con.Close();
                guncellendi = true;
            }
            catch (Exception)
            {
                guncellendi = false;
                throw;
            }
            return guncellendi;
        }
        [WebMethod]
        public bool KullaniciMobilOnayKoduDogrula(int tvmKodu, int kullanicikodu, string mobilOnayKodu)
        {
            bool Onaylandi = false;
            try
            {
                TVMKullaniciBilgi bilgi = new TVMKullaniciBilgi();
                SqlConnection con;
                con = new SqlConnection(this.ConnectionString());
                SqlCommand com = new SqlCommand("select * from TvmKullanicilar where MobilDogrulamaKodu = '" + mobilOnayKodu + "' and TVMKodu = " + tvmKodu + " and KullaniciKodu = " + kullanicikodu + "", con);
                con.Open();
                SqlDataReader dr = com.ExecuteReader();
                while (dr.Read())
                {
                    Onaylandi = true;
                }
                dr.Close();
                con.Close();

            }
            catch (Exception)
            {
                Onaylandi = false;
                throw;
            }
            return Onaylandi;
        }
        [WebMethod]
        public bool KullaniciMobilOnayKoduSifirla(int tvmKodu, int kullanicikodu, string mobilOnayKodu)
        {
            bool Sifirlandi = false;
            try
            {
                SqlConnection con;
                con = new SqlConnection(this.ConnectionString());
                SqlCommand com = new SqlCommand("update TvmKullanicilar set MobilDogrulamaKodu = 'NULL' where TVMKodu = " + tvmKodu + " and KullaniciKodu = " + kullanicikodu + " and MobilDogrulamaKodu = '" + mobilOnayKodu + "'", con);
                con.Open();
                com.ExecuteReader();
                con.Close();
                Sifirlandi = true;

            }
            catch (Exception)
            {
                Sifirlandi = false;
                throw;
            }
            return Sifirlandi;
        }

        [WebMethod]
        public List<NeoConnectMerkezProxyKullanicilari> GetMerkezOfisProxyKullanicilari()
        {
            List<NeoConnectMerkezProxyKullanicilari> list = new List<NeoConnectMerkezProxyKullanicilari>();
            con = new SqlConnection(this.ConnectionString());
            SqlCommand com = new SqlCommand("select * from NeoConnectMerkezProxyKullanicilari", con);
            con.Open();
            SqlDataReader dr = com.ExecuteReader();
            NeoConnectMerkezProxyKullanicilari model = new NeoConnectMerkezProxyKullanicilari();
            while (dr.Read())
            {
                model = new NeoConnectMerkezProxyKullanicilari();
                model.Id = Convert.ToInt32(dr["Id"].ToString());

                var sirketKodu = dr["SigortaSirketKodu"].ToString();
                if (!String.IsNullOrEmpty(sirketKodu))
                {
                    model.SigortaSirketKodu = Convert.ToInt16(dr["SigortaSirketKodu"].ToString());
                }
                var tvmKodu = dr["TVMKodu"].ToString();
                if (!String.IsNullOrEmpty(tvmKodu))
                {
                    model.TvmKodu = Convert.ToInt32(dr["TVMKodu"].ToString());
                }
                var kulKodu = dr["KullaniciKodu"].ToString();
                if (!String.IsNullOrEmpty(kulKodu))
                {
                    model.KullaniciKodu = Convert.ToInt32(dr["KullaniciKodu"].ToString());
                }
                list.Add(model);
            }
            dr.Close();
            con.Close();

            return list;
        }

        [WebMethod]
        public List<AracMarka> GetAracMarka()
        {
            List<AracMarka> list = new List<AracMarka>();
            AracMarka marka;
            try
            {
                SqlConnection con;
                con = new SqlConnection(this.ConnectionString());
                SqlCommand com = new SqlCommand("select * from AracMarka order by MarkaAdi", con);
                con.Open();
                SqlDataReader dr = com.ExecuteReader();
                while (dr.Read())
                {
                    marka = new AracMarka();
                    marka.MarkaAdi = dr["MarkaAdi"].ToString();
                    marka.MarkaKodu = dr["MarkaKodu"].ToString();
                    list.Add(marka);
                }
                dr.Close();
                con.Close();
            }
            catch (Exception ex)
            {
            }

            return list;
        }
        [WebMethod]
        public List<AracTip> GetAracTip(string res)
        {
            List<AracTip> list = new List<AracTip>();
            AracTip obje;
            try
            {
                SqlConnection con;
                con = new SqlConnection(this.ConnectionString());
                SqlCommand com = new SqlCommand("select * from AracTip where markakodu=" + res, con);
                con.Open();
                SqlDataReader dr = com.ExecuteReader();
                while (dr.Read())
                {
                    obje = new AracTip();
                    obje.TipAdi = dr["TipAdi"].ToString();
                    obje.MarkaKodu = dr["MarkaKodu"].ToString();
                    obje.TipKodu = dr["TipKodu"].ToString();
                    list.Add(obje);
                }
                dr.Close();
                con.Close();
            }
            catch (Exception ex)
            {
            }
            return list;
        }
        [WebMethod]
        public List<SirketSorulari> GetSirketSorulari(int res)
        {
            List<SirketSorulari> list = new List<SirketSorulari>();
            SirketSorulari obje;
            try
            {
                SqlConnection con;
                con = new SqlConnection(this.ConnectionString());
                SqlCommand com = new SqlCommand("select * from SirketSorulari where TumKodu=" + res, con);
                con.Open();
                SqlDataReader dr = com.ExecuteReader();
                while (dr.Read())
                {
                    obje = new SirketSorulari();
                    obje.InputTuru = dr["InputTuru"].ToString();
                    obje.TumKodu = Convert.ToInt32(dr["TumKodu"].ToString());
                    obje.SoruKodu = dr["SoruKodu"].ToString();
                    obje.Soru = dr["Soru"].ToString();
                    obje.AktifPasif = dr["AktifPasif"].ToString();
                    obje.BransKodu = Convert.ToInt32(dr["BransKodu"].ToString());
                    var KayitTarihi = (dr["KayitTarihi"].ToString());
                    if (DateTime.TryParse(KayitTarihi, out DateTime temp))
                        obje.KayitTarihi = temp;
                    obje.Id = Convert.ToInt32(dr["Id"].ToString());
                    list.Add(obje);
                }
                dr.Close();
                con.Close();
            }
            catch (Exception ex)
            {
            }
            return list;
        }
        [WebMethod]
        public List<SirketSoruDegerleri> GetSirketSoruDegerleri()
        {
            List<SirketSoruDegerleri> list = new List<SirketSoruDegerleri>();
            SirketSoruDegerleri obje;
            try
            {
                SqlConnection con;
                con = new SqlConnection(this.ConnectionString());
                SqlCommand com = new SqlCommand("select * from SirketSoruDegerleri", con);
                con.Open();
                SqlDataReader dr = com.ExecuteReader();
                while (dr.Read())
                {
                    obje = new SirketSoruDegerleri();
                    obje.Id = Convert.ToInt32(dr["Id"].ToString());
                    obje.SirketSorulariId = Convert.ToInt32(dr["SirketSorulariId"].ToString());
                    obje.Deger = dr["Deger"].ToString();
                    list.Add(obje);
                }
                dr.Close();
                con.Close();
            }
            catch (Exception ex)
            {
            }
            return list;
        }
        public string ConnectionString()
        {

            string str = "Server = tcp: neoonline.database.windows.net,1433; Initial Catalog = NeoOnline; Persist Security Info = False; User ID = NeosinerjiDBManager@neoonline; Password = NeoSnrj2017); MultipleActiveResultSets = False; Encrypt = True; TrustServerCertificate = False; Connection Timeout = 30;";

            return str;
        }

        public class TVMDetayBilgi
        {
            public int TvmKodu { get; set; }
            public int BagliOlduguTvmKodu { get; set; }
            public string MobilOnay { get; set; }
            public string HataMesaji { get; set; }
            public string IpmiMacmi { get; set; }
        }

        public class NeoConnetVersiyonNo
        {
            public int VersiyonNo { get; set; }
            public string HataMesaji { get; set; }
        }

        public class NeoConnectYasakliUrller
        {
            public int id { get; set; }
            public int TvmKodu { get; set; }
            public int AltTvmKodu { get; set; }
            public int SigortaSirketKodu { get; set; }
            public string YasaklanacakUrlleri { get; set; }
            public string HataMesaji { get; set; }
        }

        public class NeoConnectTvmYetki
        {
            public int TvmKodu { get; set; }
            public int TumKodu2 { get; set; }
            public string TumKodu { get; set; }
            public string HataMesaji { get; set; }
        }
        public class TumDetay
        {
            public int TumKodu { get; set; }
            public string Unvani { get; set; }
        }
        public class TvmDetay
        {
            public int TvmKodu { get; set; }
            public string Unvani { get; set; }
            public string WebAdresi { get; set; }
            public string Logo { get; set; }
        }
        public class NeoConnectSirketDetay
        {
            public int TUMKodu;
            public string TextTUMKodu;
            public string SigortaSirketAdi;
            public string InputTextKullaniciId;
            public string InputTextAcenteKoduId;
            public string InputTextSifreId;
            public string InputTextGirisId;
            public string LoginUrl;
            public string HataMesaji { get; set; }
        }

        public class TVMBilgi
        {
            public string KullaniciAdi { get; set; }
            public string Sifre { get; set; }
            public string AcenteKodu { get; set; }
            public string UsernameId { get; set; }
            public string AcenteKoduId { get; set; }
            public string SifreId { get; set; }
            public string GirisId { get; set; }
            public string HataMesaji { get; set; }
            public int GrupKodu { get; set; }
            public string SmsKodTelNo { get; set; }
            public string SmsKodSecretKey1 { get; set; }
            public string SmsKodSecretKey2 { get; set; }
            public string InputTextKullaniciId { get; set; }
            public string InputTextSifreId { get; set; }
            public string InputTextGirisId { get; set; }

        }
        public class NeoConnectMerkezProxyKullanicilari
        {
            public int Id { get; set; }
            public int TvmKodu { get; set; }
            public int SigortaSirketKodu { get; set; }
            public int KullaniciKodu { get; set; }
        }
        public class TVMKullaniciBilgi
        {
            public int TvmKodu { get; set; }
            public int KullaniciKodu { get; set; }
            public string CepTel { get; set; }
            public string Adi { get; set; }
            public string Soyadi { get; set; }
            public bool yetkiliKullanici { get; set; }
            public string hataMesaji { get; set; }
            public int durum { get; set; }
            public string WebAdresi { get; set; }
            public string Logo { get; set; }
        }

        public class SirketURL
        {
            public string URL { get; set; }
            public string proxyIpPort { get; set; }
            public string proxyKullaniciAdi { get; set; }
            public string proxySifre { get; set; }
            public string HataMesaji { get; set; }
        }

        public class TVMSirketProxy
        {
            public string proxyIpPort { get; set; }
            public string HataMesaji { get; set; }
        }

        public class NeoConnectLog
        {
            public int LogId { get; set; }
            public int TvmKodu { get; set; }
            public int KullaniciKodu { get; set; }
            public string Kullanici { get; set; }
            public DateTime KullaniciGirisTarihi { get; set; }
            public DateTime KullaniciCikisTarihi { get; set; }
            public string HataMesaji { get; set; }
            public string SirketKullaniciAdi { get; set; }
            public string SirketKullaniciSifresi { get; set; }
            public int GrupKodu { get; set; }
        }

        public class TVMIPBaglanti
        {
            public int TVMKodu { get; set; }
            public int SiraNo { get; set; }
            public string BaslangicIP { get; set; }
            public string BitisIP { get; set; }
            public DateTime KayitTarihi { get; set; }
            public byte Durum { get; set; }
            public string HataMesaji { get; set; }
        }
        public class NeoConnectSMSKullaniciBilgi
        {
            public int Id { get; set; }
            public int TvmKodu { get; set; }
            public int KullaniciKodu { get; set; }
            public string KullaniciAdi { get; set; }
            public string Sifre { get; set; }
            public string Gonderen { get; set; }
            public short SmsSuresi { get; set; }
            public string HataMesaji { get; set; }

        }
        public class AracMarka
        {
            [Key]
            public string MarkaKodu { get; set; }
            public string MarkaAdi { get; set; }
        }
        public class AracTip
        {
            [Key]
            public string MarkaKodu { get; set; }
            public string TipKodu { get; set; }
            public string TipAdi { get; set; }
        }
        public class SirketSoruDegerleri
        {
            [Key]
            public int Id { get; set; }
            public int? SirketSorulariId { get; set; }
            public string Deger { get; set; }
        }
        public class SirketSorulari
        {
            [Key]
            public int Id { get; set; }
            public int? TumKodu { get; set; }
            public string Soru { get; set; }
            public string InputTuru { get; set; } //select,radio,checkbox,text
            public Nullable<DateTime> KayitTarihi { get; set; }
            public string AktifPasif { get; set; }
            public int BransKodu { get; set; }
            public string SoruKodu { get; set; }//ikame_arac , meslek_adi
        }
    }
    public class TurkeyDateTime
    {
        public static DateTime Now
        {
            get
            {
                return Turkey();
            }
        }

        public static DateTime Today
        {
            get
            {
                return Turkey().Date;
            }
        }

        private static DateTime Turkey()
        {
            DateTime result = DateTime.Now;

            try
            {
                var turkeyTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Turkey Standard Time");

                result = TimeZoneInfo.ConvertTime(DateTime.Now, turkeyTimeZone);
            }
            catch (Exception ex)
            {
                throw;

            }

            return result;
        }
    }


}
