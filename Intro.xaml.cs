/*
 * Created by SharpDevelop.
 * User: David
 * Date: 2/14/2018
 * Time: 8:08 AM
 * 
 * To change this template use Tools | Options | Coding | Edit Standard Headers.
 */
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Threading.Tasks;

namespace NebliDex
{
	/// <summary>
	/// Interaction logic for Intro.xaml
	/// </summary>
	public partial class Intro : Window
	{
		public Intro()
		{
			InitializeComponent();
			WindowStartupLocation = System.Windows.WindowStartupLocation.CenterScreen;
		}
		
		private void App_Initiate(object sender, RoutedEventArgs e)
		{
		    //Create task to perform activity
		    if(App.main_window != null){return;}
		    
		    App.Start(this);
		    
		}

	}
}